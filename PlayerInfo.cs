using Godot;

namespace Gemu
{
    public struct PlayerInfo
    {
        public string Name;
        public Color Color;

        public PlayerInfo(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}
