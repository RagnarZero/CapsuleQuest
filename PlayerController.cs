using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float MoveSpeed = 8.0f;
	[Export] public float JumpVelocity = 6.0f;
	[Export] public float Gravity = 20.0f;

	[Export] public float MouseSensitivity = 0.15f;
	[Export] public float MinLookAngle = -80.0f;
	[Export] public float MaxLookAngle = 80.0f;

	private Node3D _cameraPivot;
	private Camera3D _camera;
	private float _pitchDegrees = 0.0f;

	public override void _Ready()
	{
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/Camera3D");

		bool isMine = IsMultiplayerAuthority();

		GD.Print($"{Name} ready. Local peer: {Multiplayer.GetUniqueId()}, authority: {GetMultiplayerAuthority()}, isMine: {isMine}");

		_camera.Current = isMine;
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		if (inputEvent is InputEventMouseMotion mouseMotion)
		{
			RotatePlayerWithMouse(mouseMotion.Relative);
		}

		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			GetTree().Quit();
		}

		if (inputEvent is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		Vector3 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y -= Gravity * (float)delta;
		}
		else
		{
			if (Input.IsActionJustPressed("jump"))
			{
				velocity.Y = JumpVelocity;
			}
		}

		Vector2 input = Input.GetVector(
			"move_left",
			"move_right",
			"move_forward",
            "move_backward"
		);

		Vector3 direction = Transform.Basis * new Vector3(input.X, 0, input.Y);
		direction.Y = 0;
		direction = direction.Normalized();

		if (input.Length() > 0.001f)
		{
			velocity.X = direction.X * MoveSpeed;
			velocity.Z = direction.Z * MoveSpeed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, MoveSpeed);
		}

		Velocity = velocity;
		MoveAndSlide();

		Rpc(
			nameof(SyncRemoteTransform),
			GlobalPosition,
			RotationDegrees,
			_pitchDegrees
		);
	}

	private void RotatePlayerWithMouse(Vector2 mouseDelta)
	{
		RotationDegrees = new Vector3(
			RotationDegrees.X,
			RotationDegrees.Y - mouseDelta.X * MouseSensitivity,
			RotationDegrees.Z
		);

		_pitchDegrees -= mouseDelta.Y * MouseSensitivity;
		_pitchDegrees = Mathf.Clamp(_pitchDegrees, MinLookAngle, MaxLookAngle);

		_cameraPivot.RotationDegrees = new Vector3(_pitchDegrees, 0, 0);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void SyncRemoteTransform(Vector3 position, Vector3 rotationDegrees, float pitchDegrees)
	{
		int senderId = Multiplayer.GetRemoteSenderId();

		if (senderId != GetMultiplayerAuthority())
		{
			return;
		}

		if (IsMultiplayerAuthority())
		{
			return;
		}

		GlobalPosition = position;
		RotationDegrees = rotationDegrees;

		_pitchDegrees = pitchDegrees;
		_cameraPivot.RotationDegrees = new Vector3(_pitchDegrees, 0, 0);
	}
}
