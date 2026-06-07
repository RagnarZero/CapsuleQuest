using Godot;

public partial class TerrainGenerator : Node3D
{
	[Export] public int TerrainSeed = 12345;
	[Export] public int TerrainSize = 64;
	[Export] public float CellSize = 1.0f;
	[Export] public float HeightScale = 8.0f;
	[Export] public float NoiseFrequency = 0.04f;

	[Export] public Color TerrainColor = new Color(0.2f, 0.75f, 0.25f);

	private MeshInstance3D _meshInstance;
	private Camera3D _camera;
	private DirectionalLight3D _light;
	private FastNoiseLite _noise;
	private StaticBody3D _terrainBody;

	public override void _Ready()
	{
		_meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
		//_camera = GetNode<Camera3D>("Camera3D");
		_light = GetNode<DirectionalLight3D>("DirectionalLight3D");

		SetupCameraAndLight();

		_noise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = NoiseFrequency,
			FractalOctaves = 5,
			FractalGain = 0.5f,
			Seed = TerrainSeed
		};

		GenerateTerrain();
	}

	private void SetupCameraAndLight()
	{
		return;
		_camera.Current = true;
		_camera.Position = new Vector3(0, 45, 85);
		_camera.LookAt(new Vector3(0, 0, 0), Vector3.Up);

		_light.RotationDegrees = new Vector3(-50, 30, 0);
		_light.LightEnergy = 2.5f;
	}

	private void GenerateTerrain()
	{
		Vector3[] vertices = new Vector3[(TerrainSize + 1) * (TerrainSize + 1)];
		Vector2[] uvs = new Vector2[vertices.Length];
		int[] indices = new int[TerrainSize * TerrainSize * 6];

		int vertexIndex = 0;
		float halfSize = TerrainSize * CellSize * 0.5f;

		for (int z = 0; z <= TerrainSize; z++)
		{
			for (int x = 0; x <= TerrainSize; x++)
			{
				float worldX = x * CellSize - halfSize;
				float worldZ = z * CellSize - halfSize;
				float height = GetHeight(worldX, worldZ);

				vertices[vertexIndex] = new Vector3(worldX, height, worldZ);

				uvs[vertexIndex] = new Vector2(
					x / (float)TerrainSize,
					z / (float)TerrainSize
				);

				vertexIndex++;
			}
		}

		int index = 0;

		for (int z = 0; z < TerrainSize; z++)
		{
			for (int x = 0; x < TerrainSize; x++)
			{
				int a = z * (TerrainSize + 1) + x;
				int b = a + 1;
				int c = a + TerrainSize + 1;
				int d = c + 1;

				// This is the flipped version that worked for you.
				indices[index++] = a;
				indices[index++] = b;
				indices[index++] = c;

				indices[index++] = b;
				indices[index++] = d;
				indices[index++] = c;
			}
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		arrays[(int)Mesh.ArrayType.Index] = indices;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		var surfaceTool = new SurfaceTool();
		surfaceTool.CreateFrom(mesh, 0);
		surfaceTool.GenerateNormals();

		ArrayMesh finalMesh = surfaceTool.Commit();

		var material = new StandardMaterial3D
		{
			AlbedoColor = TerrainColor,
			Roughness = 1.0f
		};

		finalMesh.SurfaceSetMaterial(0, material);

		_meshInstance.Mesh = finalMesh;

		GenerateCollision(finalMesh);
	}

	private void GenerateCollision(ArrayMesh mesh)
	{
		if (_terrainBody != null)
		{
			_terrainBody.QueueFree();
		}

		_terrainBody = new StaticBody3D
		{
			Name = "TerrainCollision"
		};

		var collisionShape = new CollisionShape3D();
		var shape = new ConcavePolygonShape3D();

		shape.SetFaces(mesh.GetFaces());
		collisionShape.Shape = shape;

		_terrainBody.AddChild(collisionShape);
		AddChild(_terrainBody);
	}

	private float GetHeight(float x, float z)
	{
		return _noise.GetNoise2D(x, z) * HeightScale;
	}
}
