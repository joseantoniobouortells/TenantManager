namespace TenantManager.Core.Services.AI;

public enum AiProcessingStage
{
    None,
    PreparingRequest,
    SendingToServer,
    WaitingForModel,
    ParsingPlan,
    ValidatingPlan,
    ExecutingQuery,
    FormattingResponse,
    Completed,
    Failed
}
