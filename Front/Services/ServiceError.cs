using System;

namespace ZipZap.Front.Services;

public abstract record ServiceError;
public sealed record Unathorized : ServiceError;
public sealed record Unknown(Exception Exception) : ServiceError;
