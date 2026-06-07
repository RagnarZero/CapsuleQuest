using Godot;

public partial class TitleScreen : Control
{
	private LineEdit _ipLineEdit;
	private Button _hostButton;
	private Button _joinButton;
	private Label _statusLabel;
	private NetworkManager _networkManager;

	public override void _Ready()
	{
		_ipLineEdit = GetNode<LineEdit>("Panel/VBoxContainer/IpLineEdit");
		_hostButton = GetNode<Button>("Panel/VBoxContainer/HostButton");
		_joinButton = GetNode<Button>("Panel/VBoxContainer/JoinButton");
		_statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");

		_networkManager = GetNode<NetworkManager>("../NetworkManager");

		_hostButton.Pressed += OnHostPressed;
		_joinButton.Pressed += OnJoinPressed;

		_ipLineEdit.Text = "127.0.0.1";
	}

	private void OnHostPressed()
	{
		_statusLabel.Text = "Hosting game...";

		bool success = _networkManager.HostGame();

		if (success)
		{
			Hide();
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else
		{
			_statusLabel.Text = "Failed to host game.";
		}
	}

	private void OnJoinPressed()
	{
		string ipAddress = _ipLineEdit.Text.Trim();

		if (string.IsNullOrEmpty(ipAddress))
		{
			_statusLabel.Text = "Please enter an IP address.";
			return;
		}

		_statusLabel.Text = $"Joining {ipAddress}...";

		bool success = _networkManager.JoinGame(ipAddress);

		if (!success)
		{
			_statusLabel.Text = "Failed to start connection.";
		}
	}

	public void ShowConnected()
	{
		Hide();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public void ShowConnectionFailed()
	{
		Show();
		_statusLabel.Text = "Connection failed.";
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public void ShowDisconnected()
	{
		Show();
		_statusLabel.Text = "Disconnected from server.";
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}
	
	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			GetTree().Quit();
		}
	}
}
