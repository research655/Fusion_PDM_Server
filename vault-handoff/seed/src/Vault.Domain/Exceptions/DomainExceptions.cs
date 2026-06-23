namespace Vault.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>Filename (case-insensitive, no extension) already exists. -> HTTP 409.</summary>
public sealed class DuplicateNameException : DomainException
{
    public DuplicateNameException(string name) : base($"A file named '{name}' already exists.") { }
}

/// <summary>The Number already exists in this vault (case-insensitive). -> HTTP 409.</summary>
public sealed class DuplicateNumberException : DomainException
{
    public DuplicateNumberException(string number) : base($"Number '{number}' is already in use.") { }
}

/// <summary>File is already checked out by someone else. -> HTTP 409.</summary>
public sealed class CheckoutConflictException : DomainException
{
    public CheckoutConflictException() : base("File is already checked out by another user.") { }
}

/// <summary>Caller lacks rights for this action (e.g., engineer self-approval, non-admin rollback). -> HTTP 403.</summary>
public sealed class ForbiddenActionException : DomainException
{
    public ForbiddenActionException(string message) : base(message) { }
}

/// <summary>Entity not found. -> HTTP 404.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string what) : base($"{what} not found.") { }
}
