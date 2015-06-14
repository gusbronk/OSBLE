﻿namespace OSBLEPlus.Logic.DomainObjects.Interfaces
{
    public interface IActivityEvent : IEventLog
    {
        int EventId { get; set; }
        string SolutionName { get; set; }
        string EventName { get; }
        string GetInsertScripts();
    }
}
