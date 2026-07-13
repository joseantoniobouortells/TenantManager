namespace TenantManager.Core.Services.AI;

public enum AiProcessingStage
{
    None,
    PreparingRequest,
    SendingToLmStudio,
    WaitingForModel,
    ParsingPlan,
    ValidatingPlan,
    ExecutingQuery,
    FormattingResponse,
    Completed,
    Failed
}
