using System;
using Model;

namespace NotEnoughRosters;

#nullable disable

public readonly struct NotEnoughRosterRowData(
    BaseLocomotive engine,
    bool isFavorite,
    bool isSelected,
    NotEnoughRosterPanel parent) : IEquatable<NotEnoughRosterRowData>
{
    public readonly BaseLocomotive Engine = engine;
    public readonly bool IsFavorite = isFavorite;
    public readonly bool IsSelected = isSelected;
    public readonly NotEnoughRosterPanel Parent = parent;

    public override int GetHashCode()
    {
        return Engine.id.GetHashCode() * 31 + IsFavorite.GetHashCode() * 31 + (Engine.trainCrewId?.GetHashCode() ?? 0);
    }

    public bool Equals(NotEnoughRosterRowData other)
    {
        return Equals(Engine, other.Engine) && IsFavorite == other.IsFavorite && IsSelected == other.IsSelected &&
               Equals(Parent, other.Parent);
    }

    public override bool Equals(object obj)
    {
        return obj is NotEnoughRosterRowData other && Equals(other);
    }
}