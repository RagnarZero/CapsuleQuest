using Godot;

public partial class TitleScreen : Control
{
	private LineEdit _nameLineEdit;
	private LineEdit _ipLineEdit;
	private Button _hostButton;
	private Button _joinButton;
	private ColorPickerButton _colorPicker;
	private Label _statusLabel;
	private NetworkManager _networkManager;

	public override void _Ready()
	{
		_nameLineEdit = GetNode<LineEdit>("Panel/VBoxContainer/NameLineEdit");
		_ipLineEdit = GetNode<LineEdit>("Panel/VBoxContainer/IpLineEdit");
		_hostButton = GetNode<Button>("Panel/VBoxContainer/HostButton");
		_joinButton = GetNode<Button>("Panel/VBoxContainer/JoinButton");
		_statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");
		_colorPicker = GetNode<ColorPickerButton>("Panel/VBoxContainer/PlayerColor");

		_networkManager = GetNode<NetworkManager>("../NetworkManager");

		_hostButton.Pressed += OnHostPressed;
		_joinButton.Pressed += OnJoinPressed;

		_nameLineEdit.Text = "Player";
		_ipLineEdit.Text = "127.0.0.1";
	}

	private void OnHostPressed()
	{
		string playerName = GetPlayerName();
		Color playerColor = GetPlayerColor();

		_statusLabel.Text = "Hosting game...";

		bool success = _networkManager.HostGame(playerName, playerColor);

		if (!success)
		{
			_statusLabel.Text = "Failed to host game.";
		}
	}

	private void OnJoinPressed()
	{
		string ipAddress = _ipLineEdit.Text.Trim();
		string playerName = GetPlayerName();
		Color playerColor = GetPlayerColor();

		if (string.IsNullOrEmpty(ipAddress))
		{
			_statusLabel.Text = "Please enter an IP address.";
			return;
		}

		_statusLabel.Text = $"Joining {ipAddress}...";

		bool success = _networkManager.JoinGame(ipAddress, playerName, playerColor);

		if (!success)
		{
			_statusLabel.Text = "Failed to start connection.";
		}
	}

	private string GetPlayerName()
	{
		string name = _nameLineEdit.Text.Trim();

		if (string.IsNullOrEmpty(name))
		{
			name = "Player";
		}

		return name;
	}

	private Color GetPlayerColor()
	{
		return _colorPicker.Color;
	}

	private Color GenerateRandomColor()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		return new Color(
			rng.RandfRange(0.2f, 1.0f),
			rng.RandfRange(0.2f, 1.0f),
			rng.RandfRange(0.2f, 1.0f)
		);
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
