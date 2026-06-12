namespace DatabaseAuditor.Application.UseCases.SyncColumns;

using System;
using System.Collections.Generic;

public class SyncColumnsCommand
{
    public Guid TargetConnectionId { get; set; }
    public List<string> SyncScripts { get; set; } = [];
}
