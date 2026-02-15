// MaybeEntity.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

namespace ZipZap.Classes.Helpers;

public abstract record MaybeEntity<T, TId>(TId Id)
where T : IEntity<TId>
where TId : IStrongId {
    public static implicit operator MaybeEntity<T, TId>(TId id) => new OnlyId<T, TId>(id);
    public static implicit operator MaybeEntity<T, TId>(T entity) => new ExistsEntity<T, TId>(entity);
    public static implicit operator TId(MaybeEntity<T, TId> maybeEntity) => maybeEntity.Id;
}

public sealed record OnlyId<T, TId>(TId Id) : MaybeEntity<T, TId>(Id)
where T : IEntity<TId>
where TId : IStrongId;

public sealed record ExistsEntity<T, TId>(T Entity) : MaybeEntity<T, TId>(Entity.Id)
where T : IEntity<TId>
where TId : IStrongId;

public static class MaybeEntityConstructor {
    extension<T, TId>(MaybeEntity<T, TId>)
        where T : IEntity<TId>
        where TId : IStrongId {
        public static MaybeEntity<T, TId> OnlyId(TId id) => new OnlyId<T, TId>(id);
        public static MaybeEntity<T, TId> ExistsEntity(T entity) => new ExistsEntity<T, TId>(entity);
    }
}
