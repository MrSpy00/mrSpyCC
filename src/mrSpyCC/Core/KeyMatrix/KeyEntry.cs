namespace mrSpyCC.Core.KeyMatrix;

public enum KeyGroup
{
    All,
    Wasd,
    Arrows,
    Numpad,
    FunctionRow,
    Numbers,
    Alphanumeric,
    Modifiers,
    Custom
}

public sealed record KeyEntry(
    string Name,
    int Offset,
    int Row,
    int Col,
    double X,
    double Y,
    double Width = 1.0,
    double Height = 1.0,
    string DisplayLabel = "",
    int VirtualKey = 0,
    bool IsExtended = false,
    KeyGroup Group = KeyGroup.Alphanumeric);
