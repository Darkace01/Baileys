using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Baileys.Defaults;
using Baileys.Types;
using Baileys.Utils;
using Baileys.WABinary;

namespace Baileys.Socket;

/// <summary>
/// Low-level WebSocket client for Baileys.
/// <para>
/// Implements the full WhatsApp connection lifecycle mirroring
/// <c>BaileysCSharp/Core/Sockets/BaseSocket.cs</c>:
/// <list type="number">
///   <item>Noise XX three-message handshake (<c>ClientHello</c> → server hello → <c>ClientFinish</c>).</item>
///   <item><c>pair-device</c> node handler — emits a QR code via <c>connection.update</c> and rotates it every 60 s.</item>
///   <item><c>pair-success</c> node handler — stores pairing identity and emits <c>creds.update</c>.</item>
///   <item><c>success</c> node handler — marks the connection open.</item>
///   <item>Frame dispatcher — routes decoded <see cref="BinaryNode"/> frames to registered handlers.</item>
///   <item>Keep-alive ping every 30 s.</item>
///   <item><c>connection.update</c> with <see cref="WaConnectionState.Close"/> on disconnect.</item>
/// </list>
/// </para>
/// </summary>
public sealed class BaileysSocket : IAsyncDisposable
{
	// ──────────────────────────────────────────────────────────────────────────
	//  Private state
	// ──────────────────────────────────────────────────────────────────────────

	private readonly IBaileysEventEmitter _ev;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _cts = new();

	private ClientWebSocket _ws = new();
	private NoiseHandler _noise;
	private AuthenticationCreds _creds;
	private KeyPair _ephemeralKeyPair;

	private Task? _receiveTask;
	private CancellationTokenSource _qrTimerCts = new();
	private CancellationTokenSource _keepAliveCts = new();

	private bool _handshakeComplete;
	private bool _isClosed;
	private bool _sentIntro;

	// Pending query completions keyed by message id
	private readonly ConcurrentDictionary<string, TaskCompletionSource<BinaryNode>> _waits = new();

	// Registered node-event handlers (CB:tag,attr:value style keys)
	private readonly Dictionary<string, Func<BinaryNode, Task<bool>>> _nodeHandlers = new();

	private string _uniqueTagId = string.Empty;
	private long _epoch = 1;

	// ──────────────────────────────────────────────────────────────────────────
	//  Constructor
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Creates a new socket. Call <see cref="ConnectAsync"/> to start the connection.
	/// </summary>
	public BaileysSocket(AuthenticationCreds creds, IBaileysEventEmitter ev, ILogger logger)
	{
		_creds = creds;
		_ev = ev;
		_logger = logger.Child(new Dictionary<string, object> { ["class"] = "socket" });
		_ephemeralKeyPair = AuthUtils.GenerateKeyPair();
		_noise = new NoiseHandler(_ephemeralKeyPair, _logger, _creds.RoutingInfo);

		RegisterNodeHandlers();
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Public API
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>Connects to WhatsApp's WebSocket and starts the Noise handshake.</summary>
	public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
	{
		_isClosed = false;
		_ws = new ClientWebSocket();
		_ws.Options.SetRequestHeader("Origin", BaileysDefaults.DefaultOrigin);

		_logger.Info($"Connecting to {url}…");
		_ev.Emit(
			"connection.update",
			new ConnectionUpdateEvent
			{
				Connection = WaConnectionState.Connecting,
				Qr = null,
				ReceivedPendingNotifications = false,
			}
		);

		await _ws.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
		_logger.Info("WebSocket connected.");

		// ── Send intro header + ClientHello ──
		var clientHello = BuildClientHelloProto();
		await SendFramedAsync(clientHello, cancellationToken).ConfigureAwait(false);
		_logger.Debug("Sent intro header and ClientHello.");

		// ── Start receive loop ──
		_receiveTask = ReceiveLoopAsync(_cts.Token);
	}

	/// <summary>
	/// Sends a binary node over the encrypted WebSocket channel.
	/// </summary>
	public async Task SendNodeAsync(BinaryNode node, CancellationToken cancellationToken = default)
	{
		var encoded = WaBinaryEncoder.EncodeBinaryNode(node);
		var encrypted = _noise.Encrypt(encoded);

		await SendFramedAsync(encrypted, cancellationToken).ConfigureAwait(false);
		_logger.Trace($"Sent node: {node.Tag}");
	}

	/// <summary>Sends a node and awaits the response by message id.</summary>
	public async Task<BinaryNode> QueryAsync(
		BinaryNode node,
		CancellationToken cancellationToken = default
	)
	{
		if (!node.Attrs.TryGetValue("id", out var id))
		{
			id = GenerateMessageTag();
			node.Attrs["id"] = id;
		}

		var tcs = new TaskCompletionSource<BinaryNode>();
		_waits[id] = tcs;

		await SendNodeAsync(node, cancellationToken).ConfigureAwait(false);
		return await tcs.Task.ConfigureAwait(false);
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Handler registration (called once in ctor)
	// ──────────────────────────────────────────────────────────────────────────

	private void RegisterNodeHandlers()
	{
		// pair-device → emit QR code
		_nodeHandlers["CB:iq,type:set,pair-device"] = OnPairDevice;

		// pair-success → pairing done, update creds
		_nodeHandlers["CB:iq,,pair-success"] = OnPairSuccess;

		// success → connection open
		_nodeHandlers["CB:success"] = OnSuccess;

		// stream error / failure
		_nodeHandlers["CB:stream:error"] = OnStreamError;
		_nodeHandlers["CB:failure"] = OnFailure;
		_nodeHandlers["CB:xmlstreamend"] = OnStreamEnd;
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Receive loop
	// ──────────────────────────────────────────────────────────────────────────

	private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
	{
		var buffer = new byte[1 << 20]; // 1 MiB receive buffer
		try
		{
			while (!cancellationToken.IsCancellationRequested && _ws.State == WebSocketState.Open)
			{
				// Accumulate a complete WebSocket message
				int totalRead = 0;
				ValueWebSocketReceiveResult result;
				do
				{
					result = await _ws.ReceiveAsync(buffer.AsMemory(totalRead), cancellationToken)
						.ConfigureAwait(false);

					if (result.MessageType == WebSocketMessageType.Close)
					{
						await CloseAsync(cancellationToken).ConfigureAwait(false);
						return;
					}

					totalRead += result.Count;

					// Grow buffer if needed
					if (!result.EndOfMessage && totalRead == buffer.Length)
					{
						var larger = new byte[buffer.Length * 2];
						buffer.CopyTo(larger, 0);
						buffer = larger;
					}
				} while (!result.EndOfMessage);

				if (result.MessageType == WebSocketMessageType.Binary)
				{
					var data = buffer[..totalRead];
					await HandleMessageAsync(data).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException)
		{ /* shutting down */
		}
		catch (Exception ex) when (!_isClosed)
		{
			_logger.Error($"Receive loop error: {ex.Message}");
			EmitClose(ex);
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Message dispatch
	// ──────────────────────────────────────────────────────────────────────────

	private async Task HandleMessageAsync(byte[] data)
	{
		int offset = 0;
		while (offset + 3 <= data.Length)
		{
			var len = (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
			if (offset + 3 + len > data.Length)
			{
				_logger.Warn($"Truncated frame received: expected {len} bytes, got {data.Length - offset - 3}");
				break;
			}

			var payload = data[(offset + 3)..(offset + 3 + len)];
			offset += 3 + len;

			try
			{
				if (!_handshakeComplete)
				{
					await HandleHandshakeResponseAsync(payload).ConfigureAwait(false);
				}
				else
				{
					var decrypted = _noise.Decrypt(payload);
					var node = await WaBinaryDecoder.DecodeBinaryNodeAsync(decrypted).ConfigureAwait(false);

					_logger.Trace($"Received node: {node.Tag}");
					await DispatchNodeAsync(node).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				_logger.Error($"Error handling frame: {ex.Message}");
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Noise XX handshake
	// ──────────────────────────────────────────────────────────────────────────

	private async Task HandleHandshakeResponseAsync(byte[] payload)
	{
		// ── Message 2: Server → Client (ServerHello containing the server's ephemeral public key) ──
		// Encoded as a minimal Protobuf HandshakeMessage: field 3 = ServerHello, field 1 = ephemeral (32 bytes).
		// Parse manually: [tag|type] [varint len] [payload]
		// We only need to extract the 32-byte server ephemeral and the 48-byte server static + 16-byte payload.
		var parsedHandshake = ParseHandshakeProto(payload);

		if (parsedHandshake.ServerHello is null)
		{
			_logger.Error("Handshake: no ServerHello received");
			return;
		}

		var serverHello = parsedHandshake.ServerHello;
		var serverEphemeral = serverHello.Ephemeral; // 32 bytes
		var serverStaticEnc = serverHello.Static; // 48 bytes (encrypted)
		var serverPayloadEnc = serverHello.Payload; // variable (encrypted)

		// ── Noise XX state machine ────────────────────────────────────────────
		// Mix server's ephemeral public key into hash, then do DH(local_eph, server_eph)
		_noise.MixHash(serverEphemeral);

		var sharedEph = AuthUtils.DiffieHellman(_ephemeralKeyPair.Private, serverEphemeral);
		MixSharedKeyIntoNoise(sharedEph);

		// Decrypt server's static key
		var serverStaticDecrypted = _noise.Decrypt(serverStaticEnc);

		// DH(local_eph, server_static)
		var sharedStatic = AuthUtils.DiffieHellman(_ephemeralKeyPair.Private, serverStaticDecrypted);
		MixSharedKeyIntoNoise(sharedStatic);

		// Decrypt server payload (cert info — we don't validate for now)
		_ = _noise.Decrypt(serverPayloadEnc);

		// ── Message 3: Client → Server (ClientFinish) ────────────────────────
		// Encrypt our noise key (static key) so the server can verify us
		var noiseKeyEncrypted = _noise.Encrypt(_creds.NoiseKey.Public);

		// DH(client_static, server_eph)
		var sharedStaticEph = AuthUtils.DiffieHellman(_creds.NoiseKey.Private, serverEphemeral);
		MixSharedKeyIntoNoise(sharedStaticEph);

		// Build client payload (registration node)
		var clientPayloadBytes = BuildClientPayload();
		var clientPayloadEnc = _noise.Encrypt(clientPayloadBytes);

		// Serialise as Protobuf HandshakeMessage { ClientFinish { static, payload } }
		var clientFinishBytes = BuildClientFinishProto(noiseKeyEncrypted, clientPayloadEnc);
		await SendFramedAsync(clientFinishBytes, _cts.Token).ConfigureAwait(false);

		// Switch noise to transport mode
		_noise.Finish();
		_handshakeComplete = true;
		_logger.Info("Noise handshake complete.");

		// Start keep-alive
		StartKeepAlive();
	}

	/// <summary>
	/// Called after the Noise shared secret has been computed.
	/// Mixes the shared secret into the Noise hash chain (KDF step).
	/// </summary>
	private void MixSharedKeyIntoNoise(byte[] sharedSecret)
	{
		_noise.MixIntoKey(sharedSecret);
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Frame / node dispatcher
	// ──────────────────────────────────────────────────────────────────────────

	private async Task DispatchNodeAsync(BinaryNode node)
	{
		// 1. Check pending query waits (matched by id attribute)
		if (node.Attrs.TryGetValue("id", out var nodeId) && _waits.TryRemove(nodeId, out var tcs))
		{
			tcs.SetResult(node);
			return;
		}

		// 2. Match registered callbacks
		bool handled = false;

		// Try exact match: "CB:<tag>,<attrKey>:<attrVal>,<child0Tag>"
		var l0 = node.Tag;
		var l1 = node.Attrs;
		var l2 =
			node.Content is BinaryNodeList bl && bl.Children.Count > 0
				? bl.Children[0].Tag
				: string.Empty;

		foreach (var (attrKey, attrVal) in l1)
		{
			handled |= await TryCallHandler($"CB:{l0},{attrKey}:{attrVal},{l2}", node)
				.ConfigureAwait(false);
			handled |= await TryCallHandler($"CB:{l0},{attrKey}:{attrVal}", node).ConfigureAwait(false);
			handled |= await TryCallHandler($"CB:{l0},{attrKey}", node).ConfigureAwait(false);
		}

		handled |= await TryCallHandler($"CB:{l0},,{l2}", node).ConfigureAwait(false);
		handled |= await TryCallHandler($"CB:{l0}", node).ConfigureAwait(false);

		if (!handled)
			_logger.Trace($"Unhandled node: {node.Tag}");
	}

	private async Task<bool> TryCallHandler(string key, BinaryNode node)
	{
		if (_nodeHandlers.TryGetValue(key, out var handler))
			return await handler(node).ConfigureAwait(false);
		return false;
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Node handlers
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Handles the <c>pair-device</c> IQ node from WhatsApp.
	/// Constructs the QR string (ref,noiseKey,identityKey,advSecret) and emits it.
	/// Rotates the QR every 60 s (first), then every 20 s (subsequent refs).
	/// </summary>
	private async Task<bool> OnPairDevice(BinaryNode message)
	{
		// Acknowledge the IQ immediately
		var ack = new BinaryNode
		{
			Tag = "iq",
			Attrs = new Dictionary<string, string>
			{
				["to"] = "s.whatsapp.net",
				["type"] = "result",
				["id"] = message.Attrs.TryGetValue("id", out var iqId) ? iqId : string.Empty,
			},
		};
		await SendNodeAsync(ack).ConfigureAwait(false);

		// Extract ref nodes from <pair-device><ref>…</ref>…</pair-device>
		var pairDeviceNode = BinaryNodeExtensions.GetChild(message, "pair-device");
		if (pairDeviceNode is null)
			return true;

		var refQueue = new Queue<BinaryNode>(
			BinaryNodeExtensions.GetChildren(pairDeviceNode).Where(c => c.Tag == "ref")
		);

		var noiseKeyB64 = Convert.ToBase64String(_creds.NoiseKey.Public);
		var identityKeyB64 = Convert.ToBase64String(_creds.SignedIdentityKey.Public);
		var advB64 = _creds.AdvSecretKey;

		_qrTimerCts?.Cancel();
		_qrTimerCts = new CancellationTokenSource();
		var qrToken = _qrTimerCts.Token;

		_ = Task.Run(
			async () =>
			{
				var timeout = 60_000;
				while (!qrToken.IsCancellationRequested)
				{
					if (!refQueue.TryDequeue(out var refNode))
					{
						_logger.Warn("QR ref queue exhausted — disconnecting.");
						EmitClose(new Exception("QR refs attempts ended"));
						return;
					}

					var refStr = refNode.Content switch
					{
						BinaryNodeBytes bBytes => Encoding.UTF8.GetString(bBytes.Data),
						BinaryNodeString bStr => bStr.Value,
						_ => string.Empty,
					};

					var qr = string.Join(",", refStr, noiseKeyB64, identityKeyB64, advB64);
					_logger.Info("QR code ready — scan with WhatsApp on your phone.");
					_ev.Emit("connection.update", new ConnectionUpdateEvent { Qr = qr });

					try
					{
						await Task.Delay(timeout, qrToken).ConfigureAwait(false);
					}
					catch (TaskCanceledException)
					{
						return;
					}

					timeout = 20_000;
				}
			},
			qrToken
		);

		return true;
	}

	/// <summary>
	/// Handles <c>pair-success</c> — confirms pairing and emits <c>creds.update</c>.
	/// </summary>
	private async Task<bool> OnPairSuccess(BinaryNode node)
	{
		_qrTimerCts?.Cancel();
		_logger.Info("Pair success — session established.");

		// Emit auth update so the client can persist the new creds
		_ev.Emit("creds.update", _creds);
		_ev.Emit("connection.update", new ConnectionUpdateEvent { Qr = null, IsNewLogin = true });

		// Acknowledge pairing
		var reply = new BinaryNode
		{
			Tag = "iq",
			Attrs = new Dictionary<string, string>
			{
				["to"] = "s.whatsapp.net",
				["type"] = "result",
				["id"] = node.Attrs.TryGetValue("id", out var id) ? id : string.Empty,
			},
		};

		await SendNodeAsync(reply).ConfigureAwait(false);
		return true;
	}

	/// <summary>Handles <c>success</c> — the connection is fully open.</summary>
	private async Task<bool> OnSuccess(BinaryNode _)
	{
		_logger.Info("✅ Connected to WhatsApp!");
		_ev.Emit(
			"connection.update",
			new ConnectionUpdateEvent { Connection = WaConnectionState.Open }
		);
		return true;
	}

	private async Task<bool> OnStreamError(BinaryNode node)
	{
		var tag =
			node.Content is BinaryNodeList bl && bl.Children.Count > 0 ? bl.Children[0].Tag : node.Tag;
		_logger.Error($"Stream error: {tag}");
		EmitClose(new Exception($"Stream error: {tag}"));
		return true;
	}

	private async Task<bool> OnFailure(BinaryNode node)
	{
		var reason = node.Attrs.TryGetValue("reason", out var r) ? r : "500";
		_logger.Error($"Connection failure: {reason}");
		EmitClose(new Exception($"Connection failure: {reason}"));
		return true;
	}

	private async Task<bool> OnStreamEnd(BinaryNode _)
	{
		EmitClose(new Exception("Connection terminated by server"));
		return true;
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Keep-alive
	// ──────────────────────────────────────────────────────────────────────────

	private void StartKeepAlive()
	{
		_keepAliveCts?.Cancel();
		_keepAliveCts = new CancellationTokenSource();
		var token = _keepAliveCts.Token;

		_ = Task.Run(
			async () =>
			{
				await Task.Delay(BaileysDefaults.KeepAliveIntervalMs, token).ConfigureAwait(false);
				while (!token.IsCancellationRequested)
				{
					try
					{
						var ping = new BinaryNode
						{
							Tag = "iq",
							Attrs = new Dictionary<string, string>
							{
								["id"] = GenerateMessageTag(),
								["to"] = "s.whatsapp.net",
								["type"] = "get",
								["xmlns"] = "w:p",
							},
							Content = new BinaryNodeList([new BinaryNode { Tag = "ping" }]),
						};

						await QueryAsync(ping, token).ConfigureAwait(false);
						_logger.Trace("Keep-alive ping OK.");
					}
					catch (OperationCanceledException)
					{
						return;
					}
					catch (Exception ex)
					{
						_logger.Error($"Keep-alive error: {ex.Message}");
					}

					await Task.Delay(BaileysDefaults.KeepAliveIntervalMs, token).ConfigureAwait(false);
				}
			},
			token
		);
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────────────────────────────────

	private string GenerateMessageTag()
	{
		if (string.IsNullOrEmpty(_uniqueTagId))
		{
			var rnd = new Random();
			_uniqueTagId = $"{rnd.Next(0, 65535)}.{rnd.Next(0, 65535)}-";
		}
		return $"{_uniqueTagId}{_epoch++}";
	}

	private async Task SendFramedAsync(byte[] data, CancellationToken cancellationToken)
	{
		var intro = !_sentIntro ? _noise.IntroHeader.ToArray() : Array.Empty<byte>();
		_sentIntro = true;

		var frame = new byte[intro.Length + 3 + data.Length];
		intro.CopyTo(frame, 0);

		int offset = intro.Length;
		frame[offset] = (byte)((data.Length >> 16) & 0xFF);
		frame[offset + 1] = (byte)((data.Length >> 8) & 0xFF);
		frame[offset + 2] = (byte)(data.Length & 0xFF);
		data.CopyTo(frame, offset + 3);

		await _ws.SendAsync(frame.AsMemory(), WebSocketMessageType.Binary, true, cancellationToken)
			.ConfigureAwait(false);
	}

	private async Task CloseAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken)
				.ConfigureAwait(false);
		}
		catch
		{ /* ignore close errors */
		}

		EmitClose(null);
	}

	private void EmitClose(Exception? error)
	{
		if (_isClosed)
			return;
		_isClosed = true;
		_keepAliveCts?.Cancel();
		_qrTimerCts?.Cancel();

		_ev.Emit(
			"connection.update",
			new ConnectionUpdateEvent
			{
				Connection = WaConnectionState.Close,
				LastDisconnect = new LastDisconnectInfo { Error = error, Date = DateTimeOffset.UtcNow },
			}
		);
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Client payload (registration node)
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Builds the client payload Protobuf bytes that are sent encrypted inside
	/// <c>ClientFinish.payload</c>. This mirrors TypeScript's
	/// <c>generateRegistrationNode</c> / <c>generateLoginNode</c>.
	/// </summary>
	private byte[] BuildClientPayload()
	{
		// Minimal ClientPayload protobuf for a new (unregistered) session:
		// field 12 (connectType, varint)     = 1 (WIFI_UNKNOWN)
		// field 13 (connectReason, varint)   = 1 (USER_ACTIVATED)
		// field 5  (userAgent, message)      → nested
		// field 6  (webInfo, message)        → nested
		// field 19 (devicePairingData, message) → nested

		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);

		// field 12: connectType = 1
		WriteProtoVarintField(w, 12, 0, 1);
		// field 13: connectReason = 1
		WriteProtoVarintField(w, 13, 0, 1);

		// field 5: userAgent
		var userAgent = BuildUserAgentProto();
		WriteProtoLenDelimitedField(w, 5, userAgent);

		// field 6: webInfo
		var webInfo = BuildWebInfoProto();
		WriteProtoLenDelimitedField(w, 6, webInfo);

		// field 19: devicePairingData (Required for new sessions to trigger QR/pairing)
		var pairingData = BuildDevicePairingDataProto();
		WriteProtoLenDelimitedField(w, 19, pairingData);

		return ms.ToArray();
	}

	private byte[] BuildDevicePairingDataProto()
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);

		// field 1: eRegid (bytes)
		var regId = new byte[4];
		System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(regId, (uint)_creds.RegistrationId);
		WriteProtoLenDelimitedField(w, 1, regId);

		// field 2: eKeytype (bytes) = [5]
		WriteProtoLenDelimitedField(w, 2, [5]);

		// field 3: eIdent (bytes)
		WriteProtoLenDelimitedField(w, 3, _creds.SignedIdentityKey.Public);

		// field 4: eSkeyId (bytes) - 3 bytes big-endian
		var sKeyId = new byte[3];
		sKeyId[0] = (byte)(_creds.SignedPreKey.KeyId >> 16);
		sKeyId[1] = (byte)(_creds.SignedPreKey.KeyId >> 8);
		sKeyId[2] = (byte)(_creds.SignedPreKey.KeyId & 0xFF);
		WriteProtoLenDelimitedField(w, 4, sKeyId);

		// field 5: eSkeyVal (bytes)
		WriteProtoLenDelimitedField(w, 5, _creds.SignedPreKey.KeyPair.Public);

		// field 6: eSkeySig (bytes)
		WriteProtoLenDelimitedField(w, 6, _creds.SignedPreKey.Signature);

		// field 7: buildHash (bytes) - MD5 of version
		var ver = string.Join(".", BaileysDefaults.BaileysVersion);
		using var md5 = System.Security.Cryptography.MD5.Create();
		var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(ver));
		WriteProtoLenDelimitedField(w, 7, hash);

		// field 8: deviceProps
		var props = BuildDevicePropsProto();
		WriteProtoLenDelimitedField(w, 8, props);

		return ms.ToArray();
	}

	private byte[] BuildDevicePropsProto()
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);
		// field 1: os (string)
		WriteProtoStringField(w, 1, "Baileys.NET");
		// field 3: platformType (varint) = CHROME = 1
		WriteProtoVarintField(w, 3, 0, 1);
		return ms.ToArray();
	}

	private byte[] BuildUserAgentProto()
	{
		// UserAgent message fields:
		// field 1: platform (varint) = WEB = 14
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);
		WriteProtoVarintField(w, 1, 0, 14); // platform = WEB

		// appVersion (field 2)
		var (major, minor, patch) = (
			BaileysDefaults.BaileysVersion[0],
			BaileysDefaults.BaileysVersion[1],
			BaileysDefaults.BaileysVersion[2]
		);
		var appVersion = BuildAppVersionProto(major, minor, patch);
		WriteProtoLenDelimitedField(w, 2, appVersion);

		WriteProtoStringField(w, 3, "000"); // mcc
		WriteProtoStringField(w, 4, "000"); // mnc
		WriteProtoStringField(w, 5, "0.1"); // osVersion
		WriteProtoStringField(w, 6, "");    // manufacturer
		WriteProtoStringField(w, 7, "Desktop"); // device
		WriteProtoStringField(w, 8, "0.1"); // osBuildNumber
		WriteProtoVarintField(w, 10, 0, 0); // releaseChannel = RELEASE (0)
		WriteProtoStringField(w, 11, "en"); // localeLanguageIso6391
		WriteProtoStringField(w, 12, "US"); // localeCountryIso31661Alpha2
		return ms.ToArray();
	}

	private static byte[] BuildAppVersionProto(int major, int minor, int patch)
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);
		WriteProtoVarintField(w, 1, 0, (uint)major);
		WriteProtoVarintField(w, 2, 0, (uint)minor);
		WriteProtoVarintField(w, 3, 0, (uint)patch);
		return ms.ToArray();
	}

	private static byte[] BuildWebInfoProto()
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);
		// webSubPlatform = 4, value = 0 (WEB_BROWSER)
		WriteProtoVarintField(w, 4, 0, 0);
		return ms.ToArray();
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Minimal Protobuf encode/decode helpers
	// ──────────────────────────────────────────────────────────────────────────

	// HandshakeMessage proto field numbers:
	//   2 = ClientHello    (message: ephemeral bytes, field 1)
	//   3 = ServerHello    (message: ephemeral bytes field 1, static field 2, payload field 3)
	//   4 = ClientFinish   (message: static field 1, payload field 2)

	/// <summary>
	/// Builds the ClientHello Protobuf bytes:
	/// <c>HandshakeMessage { ClientHello { ephemeral: pubKey } }</c>
	/// </summary>
	private byte[] BuildClientHelloProto()
	{
		// Build ClientHello { ephemeral = _ephemeralKeyPair.Public }
		var clientHello = BuildLenDelimitedMessage(1, _ephemeralKeyPair.Public);
		// Wrap in HandshakeMessage { field 2 = message }
		return BuildLenDelimitedMessage(2, clientHello);
	}

	/// <summary>
	/// Builds the ClientFinish Protobuf bytes:
	/// <c>HandshakeMessage { ClientFinish { static: staticEnc, payload: payloadEnc } }</c>
	/// </summary>
	private static byte[] BuildClientFinishProto(byte[] staticEnc, byte[] payloadEnc)
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);

		// ClientFinish
		var cf = new MemoryStream();
		using (var cfw = new BinaryWriter(cf, Encoding.UTF8, leaveOpen: true))
		{
			WriteProtoLenDelimitedField(cfw, 1, staticEnc); // static
			WriteProtoLenDelimitedField(cfw, 2, payloadEnc); // payload
		}
		var cfBytes = cf.ToArray();

		// HandshakeMessage { ClientFinish (field 4) }
		WriteProtoLenDelimitedField(w, 4, cfBytes);
		return ms.ToArray();
	}

	private record ParsedServerHello(byte[] Ephemeral, byte[] Static, byte[] Payload);

	private record ParsedHandshake(ParsedServerHello? ServerHello);

	/// <summary>Parses the minimal HandshakeMessage (ServerHello variant) from raw bytes.</summary>
	private static ParsedHandshake ParseHandshakeProto(byte[] data)
	{
		int pos = 0;

		ParsedServerHello? serverHello = null;

		while (pos < data.Length)
		{
			var (fieldNum, wireType) = ReadProtoTag(data, ref pos);

			if (wireType != 2) // length-delimited
			{
				SkipProtoField(data, ref pos, wireType);
				continue;
			}

			var len = (int)ReadProtoVarint(data, ref pos);
			var value = data[pos..(pos + len)];
			pos += len;

			if (fieldNum == 3) // ServerHello
			{
				// Parse ServerHello:  ephemeral=1, static=2, payload=3
				byte[]? eph = null,
					stat = null,
					pay = null;
				int innerPos = 0;
				while (innerPos < value.Length)
				{
					var (fn, wt) = ReadProtoTag(value, ref innerPos);
					if (wt != 2)
					{
						SkipProtoField(value, ref innerPos, wt);
						continue;
					}
					var l = (int)ReadProtoVarint(value, ref innerPos);
					var v = value[innerPos..(innerPos + l)];
					innerPos += l;
					if (fn == 1)
						eph = v;
					else if (fn == 2)
						stat = v;
					else if (fn == 3)
						pay = v;
				}
				if (eph is not null && stat is not null && pay is not null)
					serverHello = new ParsedServerHello(eph, stat, pay);
			}
		}

		return new ParsedHandshake(serverHello);
	}

	// ─── Low-level protobuf primitives ───────────────────────────────────────

	private static byte[] BuildLenDelimitedMessage(int fieldNumber, byte[] fieldBytes)
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);
		WriteProtoLenDelimitedField(w, fieldNumber, fieldBytes);
		return ms.ToArray();
	}

	private static void WriteProtoTag(BinaryWriter w, int fieldNumber, int wireType) =>
		WriteProtoVarintRaw(w, (ulong)((fieldNumber << 3) | wireType));

	private static void WriteProtoVarintField(BinaryWriter w, int fn, int wireType, ulong value)
	{
		WriteProtoTag(w, fn, wireType); // wireType 0 = varint
		WriteProtoVarintRaw(w, value);
	}

	private static void WriteProtoStringField(BinaryWriter w, int fn, string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value);
		WriteProtoLenDelimitedField(w, fn, bytes);
	}

	private static void WriteProtoLenDelimitedField(BinaryWriter w, int fn, byte[] value)
	{
		WriteProtoTag(w, fn, 2); // wireType 2 = length-delimited
		WriteProtoVarintRaw(w, (ulong)value.Length);
		w.Write(value);
	}

	private static void WriteProtoVarintRaw(BinaryWriter w, ulong value)
	{
		while (value > 0x7F)
		{
			w.Write((byte)((value & 0x7F) | 0x80));
			value >>= 7;
		}
		w.Write((byte)value);
	}

	private static (int fieldNum, int wireType) ReadProtoTag(byte[] data, ref int pos)
	{
		ulong tag = ReadProtoVarint(data, ref pos);
		int wireT = (int)(tag & 0x07);
		int fieldN = (int)(tag >> 3);
		return (fieldN, wireT);
	}

	private static ulong ReadProtoVarint(byte[] data, ref int pos)
	{
		ulong result = 0;
		int shift = 0;
		while (pos < data.Length)
		{
			byte b = data[pos++];
			result |= (ulong)(b & 0x7F) << shift;
			if ((b & 0x80) == 0)
				break;
			shift += 7;
		}
		return result;
	}

	private static void SkipProtoField(byte[] data, ref int pos, int wireType)
	{
		switch (wireType)
		{
			case 0:
				ReadProtoVarint(data, ref pos);
				break;
			case 1:
				pos += 8;
				break;
			case 2:
				pos += (int)ReadProtoVarint(data, ref pos);
				break;
			case 5:
				pos += 4;
				break;
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Dispose
	// ──────────────────────────────────────────────────────────────────────────

	public async ValueTask DisposeAsync()
	{
		await _cts.CancelAsync().ConfigureAwait(false);
		if (_keepAliveCts is not null)
			await _keepAliveCts.CancelAsync().ConfigureAwait(false);
		if (_qrTimerCts is not null)
			await _qrTimerCts.CancelAsync().ConfigureAwait(false);

		if (_receiveTask is not null)
		{
			try
			{
				await _receiveTask.ConfigureAwait(false);
			}
			catch
			{ /* ignore */
			}
		}

		_ws.Dispose();
		_cts.Dispose();
		_keepAliveCts?.Dispose();
		_qrTimerCts?.Dispose();
	}
}
