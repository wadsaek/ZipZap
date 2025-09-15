using System;

namespace ZipZap.Classes;

public interface IEntity<T>
    where T : IEquatable<T> {
    public T Id { get; set; }
}
