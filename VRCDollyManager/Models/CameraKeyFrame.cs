namespace VRCDollyManager.Models;

public class CameraKeyFrame
{
    public int Index { get; set; }
    public int PathIndex { get; set; }
    public double FocalDistance { get; set; }
    public double Aperture { get; set; }
    public double Hue { get; set; }
    public double Saturation { get; set; }
    public double Lightness { get; set; }
    public double LookAtMeXOffset { get; set; }
    public double LookAtMeYOffset { get; set; }
    public double Zoom { get; set; }
    public double Speed { get; set; }
    public double Duration { get; set; }
    public Position Position { get; set; } = new();
    public Rotation Rotation { get; set; } = new();
    public bool IsLocal { get; set; } = false;
}

public class Position
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class Rotation
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}