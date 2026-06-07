using Godot;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
	[Export] public PackedScene PlayerScene;
	[Export] public int Port = 7777;
	[Export] public int MaxPlayers = 16;

	private readonly Dictionary<int, CharacterBody3D> _players = new();

	private TitleScreen _titleScreen;

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

	public bool HostGame()
	{
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

		SpawnPlayer(myId);

		return true;
	}

	public bool JoinGame(string address)
	{
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
		GD.Print($"Connected to server. My peer id is {Multiplayer.GetUniqueId()}");

		// Do NOT hide the title screen here.
		// We only hide it once the local player's camera actually exists.
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

		// Tell the newly connected client about all players that already exist.
		foreach (int existingPeerId in _players.Keys)
		{
			RpcId(newPeerId, nameof(SpawnPlayerOnAllPeers), existingPeerId);
		}

		// Tell everyone about the newly connected player.
		Rpc(nameof(SpawnPlayerOnAllPeers), newPeerId);
	}

	private void OnPeerDisconnected(long peerId)
	{
		GD.Print($"Peer disconnected: {peerId}");

		if (!Multiplayer.IsServer())
		{
			return;
		}

		Rpc(nameof(RemovePlayerOnAllPeers), (int)peerId);
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

		// Important: set authority BEFORE AddChild, so _Ready() sees the correct authority.
		player.SetMultiplayerAuthority(peerId);

		AddChild(player);

		player.GlobalPosition = new Vector3((peerId - 1) * 2.5f, 25.0f, 0.0f);

		_players[peerId] = player;

		GD.Print($"Spawned player for peer {peerId} on local peer {Multiplayer.GetUniqueId()}");

		// This is the important part:
		// only hide the menu once THIS machine's own player exists.
		if (peerId == Multiplayer.GetUniqueId())
		{
			_titleScreen?.ShowConnected();
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}
}
