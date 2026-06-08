using Gemu;
using Godot;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
	[Export] public PackedScene PlayerScene;
	[Export] public int Port = 7777;
	[Export] public int MaxPlayers = 16;

	private readonly Dictionary<int, CharacterBody3D> _players = new();
	private readonly Dictionary<int, PlayerInfo> _playerInfos = new();

	private TitleScreen _titleScreen;

	private string _localPlayerName = "Player";
	private Color _localPlayerColor = Colors.White;

	public override void _Ready()
	{
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;

		_titleScreen = GetNodeOrNull<TitleScreen>("../TitleScreen");

		Input.MouseMode = Input.MouseModeEnum.Visible;

		GD.Print("NetworkManager ready.");
	}

	public bool HostGame(string playerName, Color playerColor)
	{
		_localPlayerName = playerName;
		_localPlayerColor = playerColor;

		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(Port, MaxPlayers);

		if (error != Error.Ok)
		{
			GD.PrintErr($"Failed to host game: {error}");
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;

		int myId = Multiplayer.GetUniqueId();

		GD.Print($"Hosting game. My peer id is {myId}");

		_playerInfos[myId] = new PlayerInfo(playerName, playerColor);

		SpawnPlayer(myId);
		ApplyPlayerInfo(myId, playerName, playerColor);

		_titleScreen?.ShowConnected();

		return true;
	}

	public bool JoinGame(string address, string playerName, Color playerColor)
	{
		_localPlayerName = playerName;
		_localPlayerColor = playerColor;

		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateClient(address, Port);

		if (error != Error.Ok)
		{
			GD.PrintErr($"Failed to join game: {error}");
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;

		GD.Print($"Trying to join {address}:{Port}");

		return true;
	}

	private void OnConnectedToServer()
	{
		int myId = Multiplayer.GetUniqueId();

		GD.Print($"Connected to server. My peer id is {myId}");

		RpcId(
			1,
			nameof(RegisterPlayerInfoOnServer),
			_localPlayerName,
			_localPlayerColor
		);
	}

	private void OnConnectionFailed()
	{
		GD.PrintErr("Connection failed.");
		_titleScreen?.ShowConnectionFailed();
	}

	private void OnServerDisconnected()
	{
		GD.Print("Disconnected from server.");
		_titleScreen?.ShowDisconnected();
	}

	private void OnPeerConnected(long peerId)
	{
		GD.Print($"Peer connected: {peerId}");

		if (!Multiplayer.IsServer())
		{
			return;
		}

		int newPeerId = (int)peerId;

		// Send existing players to the new client.
		foreach (int existingPeerId in _players.Keys)
		{
			RpcId(newPeerId, nameof(SpawnPlayerOnAllPeers), existingPeerId);
		}

		// Send existing player info to the new client.
		foreach (var pair in _playerInfos)
		{
			RpcId(
				newPeerId,
				nameof(ApplyPlayerInfoOnAllPeers),
				pair.Key,
				pair.Value.Name,
				pair.Value.Color
			);
		}

		// Spawn the new player for everyone.
		Rpc(nameof(SpawnPlayerOnAllPeers), newPeerId);
	}

	private void OnPeerDisconnected(long peerId)
	{
		GD.Print($"Peer disconnected: {peerId}");

		if (!Multiplayer.IsServer())
		{
			return;
		}

		int disconnectedPeerId = (int)peerId;

		_playerInfos.Remove(disconnectedPeerId);

		Rpc(nameof(RemovePlayerOnAllPeers), disconnectedPeerId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	public void RegisterPlayerInfoOnServer(string playerName, Color playerColor)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		int senderId = Multiplayer.GetRemoteSenderId();

		GD.Print($"Received player info from {senderId}: {playerName}");

		_playerInfos[senderId] = new PlayerInfo(playerName, playerColor);

		Rpc(
			nameof(ApplyPlayerInfoOnAllPeers),
			senderId,
			playerName,
			playerColor
		);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void SpawnPlayerOnAllPeers(int peerId)
	{
		GD.Print($"RPC spawn received for peer {peerId} on local peer {Multiplayer.GetUniqueId()}");
		SpawnPlayer(peerId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void RemovePlayerOnAllPeers(int peerId)
	{
		if (!_players.TryGetValue(peerId, out CharacterBody3D player))
		{
			return;
		}

		player.QueueFree();
		_players.Remove(peerId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void ApplyPlayerInfoOnAllPeers(int peerId, string playerName, Color playerColor)
	{
		ApplyPlayerInfo(peerId, playerName, playerColor);
	}

	private void SpawnPlayer(int peerId)
	{
		if (_players.ContainsKey(peerId))
		{
			return;
		}

		if (PlayerScene == null)
		{
			GD.PrintErr("PlayerScene is not assigned on NetworkManager.");
			return;
		}

		var player = PlayerScene.Instantiate<CharacterBody3D>();

		player.Name = $"Player_{peerId}";
		player.SetMultiplayerAuthority(peerId);

		AddChild(player);

		int spawnIndex = _players.Count;
		player.GlobalPosition = new Vector3(spawnIndex * 2.5f, 25.0f, 0.0f);

		_players[peerId] = player;

		GD.Print($"Spawned player for peer {peerId} on local peer {Multiplayer.GetUniqueId()} at {player.GlobalPosition}");

		if (_playerInfos.TryGetValue(peerId, out PlayerInfo info))
		{
			ApplyPlayerInfo(peerId, info.Name, info.Color);
		}

		if (peerId == Multiplayer.GetUniqueId())
		{
			_titleScreen?.ShowConnected();
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	private void ApplyPlayerInfo(int peerId, string playerName, Color playerColor)
	{
		if (!_players.TryGetValue(peerId, out CharacterBody3D player))
		{
			GD.Print($"Player {peerId} not spawned yet. Cannot apply info.");
			return;
		}

		if (player is PlayerController playerController)
		{
			playerController.SetDisplayName(playerName);
			playerController.SetColor(playerColor);
		}
	}
}
