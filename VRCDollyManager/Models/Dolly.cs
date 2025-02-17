using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Extensions;

namespace VRCDollyManager.Models;

public class Dolly : IEquatable<Dolly>
{
    [Key] public int Id { get; set; }

    [Required] [MaxLength(255)] public string Name { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    [NotMapped] public List<string> Tags { get; set; } = new();

    [NotMapped] public List<CameraKeyFrame> KeyFrames { get; set; } = new();

    public string TagsString
    {
        get => string.Join(",", Tags);
        set => Tags = string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(Alias) ? Name : Alias;
    }


    public override bool Equals(object? obj)
    {
        return obj is Dolly other && Equals(other);
    }

    public bool Equals(Dolly? other)
    {
        if (other is null)
            return false;

        return Name == other.Name;
    }

    public override int GetHashCode()
    {
        unchecked // Allow overflow for better distribution
        {
            var hash = 17;
            hash = hash * 23 + Name.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(Dolly? left, Dolly? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Dolly? left, Dolly? right)
    {
        return !(left == right);
    }

    public Dolly Clone()
    {
        return new Dolly
        {
            Name = Name,
            Alias = Alias,
            Tags = new List<string>(Tags),
            KeyFrames = new List<CameraKeyFrame>(KeyFrames)
        };
    }


}