using Baileys.Defaults;
using Baileys.Extensions;
using Baileys.Options;
using Baileys.Session;
using Baileys.Socket;
using Baileys.Types;
using Baileys.Utils;
using Microsoft.Extensions.Options;

namespace Baileys;

/// <summary>
/// High-level client for interacting with WhatsApp Web.
/// <para>
/// This class orchestrates the full session lifecycle, mirroring how
/// <c>WhatsSocketConsole/Program.cs</c> in BaileysCSharp manages connections:
/// <list type="number">
///   <item>Loads (or initialises) <see cref="AuthenticationCreds"/> via the configured <see cref="IAuthStateProvider"/>.</item>
///   <item>Passes the creds to a new <see cref="BaileysSocket"/> that performs the Noise handshake.</item>
///   <item>Subscribes to <c>creds.update</c> events and persists updated credentials automatically.</item>
///   <item>Prints the QR code to the terminal when <c>options.PrintQrInTerminal</c> is set.</item>
///   <item>Automatically reconnects on non-logout disconnects (mirrors <c>socket.MakeSocket()</c> re-call).</item>
/// </list>
/// </para>
/// </summary>
public sealed class BaileysClient : IAsyncDisposable
{
	private readonly IAuthStateProvider _authStateProvider;
	private readonly IBaileysEventEmitter _ev;
	private readonly BaileysOptions _options;
	private readonly ILogger _logger;

	private BaileysSocket? _socket;
	private AuthenticationCreds? _creds;
	private bool _loggedOut;

	public BaileysClient(
		IAuthStateProvider authStateProvider,
		IBaileysEventEmitter ev,
		IOptions<BaileysOptions> options,
		ILogger logger
	)
	{
		_authStateProvider = authStateProvider;
		_ev = ev;
		_options = options.Value;
		_logger = logger.Child(new Dictionary<string, object> { ["class"] = "client" });
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Public API
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>Returns the event emitter for this client.</summary>
	public IBaileysEventEmitter Ev => _ev;

	/// <summary>
	/// Initiates a connection to WhatsApp.
	/// Subscribes to <c>connection.update</c>, <c>creds.update</c> events,
	/// then starts the socket.
	/// </summary>
	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		// Load or initialise credentials
		_creds = await _authStateProvider.LoadCredsAsync(cancellationToken).ConfigureAwait(false);

		// Subscribe to lifecycle events (once per client lifetime)
		_ev.On<ConnectionUpdateEvent>("connection.update", update => _ = OnConnectionUpdate(update));
		_ev.On<AuthenticationCreds>("creds.update", creds => _ = OnCredsUpdate(creds));

		await MakeSocketAsync(cancellationToken).ConfigureAwait(false);
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Private helpers
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Creates a new socket instance and starts the connection.
	/// Called both on first connect and on auto-reconnect.
	/// </summary>
	private async Task MakeSocketAsync(CancellationToken cancellationToken = default)
	{
		if (_socket is not null)
			await _socket.DisposeAsync().ConfigureAwait(false);

		_socket = new BaileysSocket(_creds!, _ev, _logger);
		await _socket
			.ConnectAsync(BaileysDefaults.WaWebSocketUrl, cancellationToken)
			.ConfigureAwait(false);
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Event handlers
	// ──────────────────────────────────────────────────────────────────────────

	private async Task OnConnectionUpdate(ConnectionUpdateEvent update)
	{
		// Print QR to terminal when requested
		if (_options.PrintQrInTerminal && update.Qr is { } qr)
			QrUtils.LogQr(qr, _logger);

		if (update.Connection == WaConnectionState.Open)
		{
			_logger.Info("✅ Connected to WhatsApp!");
		}
		else if (update.Connection == WaConnectionState.Close)
		{
			var error = update.LastDisconnect?.Error;
			_logger.Warn($"❌ Disconnected: {error?.Message ?? "unknown reason"}");

			// Check if logged out (status 401)
			bool isLogout =
				error?.Message?.Contains("401") == true || error?.Message?.Contains("LoggedOut") == true;

			if (isLogout)
			{
				_loggedOut = true;
				_logger.Warn("You are logged out. Clear credentials and restart to reconnect.");
				await _authStateProvider.ClearAsync().ConfigureAwait(false);
				return;
			}

			if (!_loggedOut)
			{
				// Auto-reconnect after a short delay (mirrors BaileysCSharp Thread.Sleep(1000))
				try
				{
					await Task.Delay(1_000).ConfigureAwait(false);
					_logger.Info("Reconnecting…");
					await MakeSocketAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_logger.Error($"Reconnect failed: {ex.Message}");
				}
			}
		}
	}

	/// <summary>
	/// Fired when credentials change (after pairing or session update).
	/// Persists them via the configured <see cref="IAuthStateProvider"/>.
	/// Mirrors the TypeScript <c>saveCreds()</c> callback.
	/// </summary>
	private async Task OnCredsUpdate(AuthenticationCreds updatedCreds)
	{
		_creds = updatedCreds;
		try
		{
			await _authStateProvider.SaveCredsAsync(updatedCreds).ConfigureAwait(false);
			_logger.Debug("Credentials saved.");
		}
		catch (Exception ex)
		{
			_logger.Error($"Failed to save credentials: {ex.Message}");
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Dispose
	// ──────────────────────────────────────────────────────────────────────────

	public async ValueTask DisposeAsync()
	{
		if (_socket is not null)
			await _socket.DisposeAsync().ConfigureAwait(false);
	}
}
