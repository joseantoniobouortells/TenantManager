using System;
using TenantManager.Core.Services.AI;

namespace TenantManager.Core.Services.AI;

public interface IAssistantExecutionObserver
{
    void OnRequestReceived(string userMessage);
    void OnPlanGenerated(SemanticQueryPlan plan);
    void OnQueryExecuted(bool success);
    void OnResponseFormatted(string finalAnswer);
    void OnPeriodResolved(int? year, int? month);
}
