using Vintagestory.API.Common;

namespace RecipesLibrary;

internal static class Extensions
{
    public static void ToBytes(this ModelTransform transform, BinaryWriter writer)
    {
        writer.Write(transform.Translation.X);
        writer.Write(transform.Translation.Y);
        writer.Write(transform.Translation.Z);
        writer.Write(transform.Rotation.X);
        writer.Write(transform.Rotation.Y);
        writer.Write(transform.Rotation.Z);
        writer.Write(transform.Origin.X);
        writer.Write(transform.Origin.Y);
        writer.Write(transform.Origin.Z);
        writer.Write(transform.ScaleXYZ.X);
        writer.Write(transform.ScaleXYZ.Y);
        writer.Write(transform.ScaleXYZ.Z);
        writer.Write(transform.Rotate);
    }
    public static void FromBytes(this ModelTransform transform, BinaryReader reader)
    {
        transform.Translation.X = reader.ReadSingle();
        transform.Translation.Y = reader.ReadSingle();
        transform.Translation.Z = reader.ReadSingle();
        transform.Rotation.X = reader.ReadSingle();
        transform.Rotation.Y = reader.ReadSingle();
        transform.Rotation.Z = reader.ReadSingle();
        transform.Origin.X = reader.ReadSingle();
        transform.Origin.Y = reader.ReadSingle();
        transform.Origin.Z = reader.ReadSingle();
        transform.ScaleXYZ.X = reader.ReadSingle();
        transform.ScaleXYZ.Y = reader.ReadSingle();
        transform.ScaleXYZ.Z = reader.ReadSingle();
        transform.Rotate = reader.ReadBoolean();
    }
}
