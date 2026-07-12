using System;
using System.Collections.Generic;
using System.Linq;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Deterministically formats query results into user-friendly localized text answers.
/// Handles counts, lists, sums, lookup results, and error states.
/// </summary>
public static class SemanticAnswerFormatter
{
    public static string Format(SemanticQueryPlan plan, object? queryResult, string language)
    {
        bool isEs = language.Equals("es", StringComparison.OrdinalIgnoreCase);

        if (queryResult == null)
        {
            return isEs
                ? "No se encontraron datos que coincidan con su consulta."
                : "No data was found matching your query.";
        }

        if (!plan.Operation.HasValue || !plan.Resource.HasValue)
        {
            return isEs 
                ? "Operación no soportada por el formateador." 
                : "Operation not supported by the formatter.";
        }

        return plan.Operation.Value switch
        {
            SemanticQueryOperation.Count => FormatCount(plan, Convert.ToInt32(queryResult), isEs),
            SemanticQueryOperation.Sum => FormatSum(plan, Convert.ToDecimal(queryResult), isEs),
            SemanticQueryOperation.Summary => FormatSummary(plan, queryResult, isEs),
            SemanticQueryOperation.Lookup => FormatLookup(plan, queryResult, isEs),
            SemanticQueryOperation.List => FormatList(plan, queryResult, isEs),
            _ => isEs 
                ? "Operación no soportada por el formateador." 
                : "Operation not supported by the formatter."
        };
    }

    private static string FormatCount(SemanticQueryPlan plan, int count, bool isEs)
    {
        var resource = plan.Resource!.Value;
        bool isLate = plan.Filters.Any(f => f.Field.Equals("late", StringComparison.OrdinalIgnoreCase) && f.Value != null && Convert.ToBoolean(f.Value));
        bool isPending = plan.Filters.Any(f => f.Field.Equals("pending", StringComparison.OrdinalIgnoreCase) && f.Value != null && Convert.ToBoolean(f.Value));
        bool isActive = plan.Filters.Any(f => f.Field.Equals("active", StringComparison.OrdinalIgnoreCase) && f.Value != null && Convert.ToBoolean(f.Value));
        bool isAvailable = plan.Filters.Any(f => f.Field.Equals("available", StringComparison.OrdinalIgnoreCase) && f.Value != null && Convert.ToBoolean(f.Value));

        if (isEs)
        {
            if (count == 0)
            {
                if (resource == SemanticQueryResource.Payments && isLate) return "No hay pagos con retraso.";
                if (resource == SemanticQueryResource.Payments && isPending) return "No hay pagos pendientes.";
                if (resource == SemanticQueryResource.Contracts && isActive) return "No hay contratos activos.";
                if (resource == SemanticQueryResource.Rooms && isAvailable) return "No hay habitaciones libres.";
                return $"No hay {GetResourcePluralEs(resource)}.";
            }

            if (count == 1)
            {
                if (resource == SemanticQueryResource.Payments && isLate) return "Hay 1 pago con retraso.";
                if (resource == SemanticQueryResource.Payments && isPending) return "Hay 1 pago pendiente.";
                if (resource == SemanticQueryResource.Contracts && isActive) return "Hay 1 contrato activo.";
                if (resource == SemanticQueryResource.Rooms && isAvailable) return "Hay 1 habitación libre.";
                return $"Hay 1 {GetResourceSingularEs(resource)}.";
            }

            if (resource == SemanticQueryResource.Payments && isLate) return $"Hay {count} pagos con retraso.";
            if (resource == SemanticQueryResource.Payments && isPending) return $"Hay {count} pagos pendientes.";
            if (resource == SemanticQueryResource.Contracts && isActive) return $"Hay {count} contratos activos.";
            if (resource == SemanticQueryResource.Rooms && isAvailable) return $"Hay {count} habitaciones libres.";
            return $"Hay {count} {GetResourcePluralEs(resource)}.";
        }
        else
        {
            if (count == 0)
            {
                if (resource == SemanticQueryResource.Payments && isLate) return "There are no late payments.";
                if (resource == SemanticQueryResource.Payments && isPending) return "There are no pending payments.";
                if (resource == SemanticQueryResource.Contracts && isActive) return "There are no active contracts.";
                if (resource == SemanticQueryResource.Rooms && isAvailable) return "There are no available rooms.";
                return $"There are no {GetResourcePluralEn(resource)}.";
            }

            if (count == 1)
            {
                if (resource == SemanticQueryResource.Payments && isLate) return "There is 1 late payment.";
                if (resource == SemanticQueryResource.Payments && isPending) return "There is 1 pending payment.";
                if (resource == SemanticQueryResource.Contracts && isActive) return "There is 1 active contract.";
                if (resource == SemanticQueryResource.Rooms && isAvailable) return "There is 1 available room.";
                return $"There is 1 {GetResourceSingularEn(resource)}.";
            }

            if (resource == SemanticQueryResource.Payments && isLate) return $"There are {count} late payments.";
            if (resource == SemanticQueryResource.Payments && isPending) return $"There are {count} pending payments.";
            if (resource == SemanticQueryResource.Contracts && isActive) return $"There are {count} active contracts.";
            if (resource == SemanticQueryResource.Rooms && isAvailable) return $"There are {count} available rooms.";
            return $"There are {count} {GetResourcePluralEn(resource)}.";
        }
    }

    private static string FormatSum(SemanticQueryPlan plan, decimal sum, bool isEs)
    {
        var resource = plan.Resource!.Value;
        bool isPending = plan.Filters.Any(f => f.Field.Equals("pending", StringComparison.OrdinalIgnoreCase) && f.Value != null && Convert.ToBoolean(f.Value));
        bool isCurrent = plan.Filters.Any(f => (f.Field.Equals("month", StringComparison.OrdinalIgnoreCase) || f.Field.Equals("year", StringComparison.OrdinalIgnoreCase)) && f.Value != null && f.Value.ToString()!.Equals("current", StringComparison.OrdinalIgnoreCase));

        if (isEs)
        {
            if (resource == SemanticQueryResource.Payments && isPending)
            {
                return isCurrent 
                    ? $"Queda por cobrar un total de {sum:N2} € de pagos pendientes este mes." 
                    : $"Queda por cobrar un total de {sum:N2} € de pagos pendientes.";
            }
            return $"El total sumado es {sum:N2} €.";
        }
        else
        {
            if (resource == SemanticQueryResource.Payments && isPending)
            {
                return isCurrent
                    ? $"There is a total of {sum:N2} € pending to collect this month."
                    : $"There is a total of {sum:N2} € pending to collect.";
            }
            return $"The total sum is {sum:N2} €.";
        }
    }

    private static string FormatSummary(SemanticQueryPlan plan, object result, bool isEs)
    {
        if (result is SemanticDashboardResult dashboard)
        {
            return isEs
                ? $"Resumen de la propiedad: {dashboard.RoomCount} habitaciones ({dashboard.ActiveTenantsCount} ocupadas), {dashboard.PendingPaymentsCount} pagos pendientes ({dashboard.LatePaymentsCount} con retraso)."
                : $"Property summary: {dashboard.RoomCount} rooms ({dashboard.ActiveTenantsCount} occupied), {dashboard.PendingPaymentsCount} pending payments ({dashboard.LatePaymentsCount} late).";
        }
        return result.ToString() ?? "";
    }

    private static string FormatLookup(SemanticQueryPlan plan, object result, bool isEs)
    {
        if (result is SemanticTenantResult tenant)
        {
            if (isEs)
            {
                if (tenant.EffectiveMoveOutDate.HasValue)
                {
                    return $"{tenant.FullName} tiene previsto dejar la habitación el {tenant.EffectiveMoveOutDate.Value:yyyy-MM-dd}.";
                }
                if (!string.IsNullOrWhiteSpace(tenant.CurrentRoom))
                {
                    return $"{tenant.FullName} está actualmente en la habitación {tenant.CurrentRoom}.";
                }
                return $"{tenant.FullName} (Inquilino activo: {(tenant.Active ? "Sí" : "No")}).";
            }
            else
            {
                if (tenant.EffectiveMoveOutDate.HasValue)
                {
                    return $"{tenant.FullName} is scheduled to move out on {tenant.EffectiveMoveOutDate.Value:yyyy-MM-dd}.";
                }
                if (!string.IsNullOrWhiteSpace(tenant.CurrentRoom))
                {
                    return $"{tenant.FullName} is currently in room {tenant.CurrentRoom}.";
                }
                return $"{tenant.FullName} (Active tenant: {(tenant.Active ? "Yes" : "No")}).";
            }
        }
        return result.ToString() ?? "";
    }

    private static string FormatList(SemanticQueryPlan plan, object result, bool isEs)
    {
        var resource = plan.Resource!.Value;
        if (result is System.Collections.IEnumerable list)
        {
            var items = list.Cast<object>().ToList();
            if (!items.Any())
            {
                return isEs 
                    ? $"No se encontraron registros de {GetResourcePluralEs(resource)} que coincidan con los criterios." 
                    : $"No {GetResourcePluralEn(resource)} found matching those criteria.";
            }

            if (resource == SemanticQueryResource.Rooms)
            {
                var names = items.Cast<SemanticRoomResult>().Select(r => r.Name).ToList();
                bool isAvailable = plan.Filters.Any(f => f.Field.Equals("available", StringComparison.OrdinalIgnoreCase) && f.Value != null && Convert.ToBoolean(f.Value));
                if (isEs)
                {
                    return isAvailable 
                        ? $"Las habitaciones libres son: {string.Join(", ", names)}." 
                        : $"Las habitaciones son: {string.Join(", ", names)}.";
                }
                else
                {
                    return isAvailable 
                        ? $"The available rooms are: {string.Join(", ", names)}." 
                        : $"The rooms are: {string.Join(", ", names)}.";
                }
            }

            if (resource == SemanticQueryResource.Tenants)
            {
                var names = items.Cast<SemanticTenantResult>().Select(t => t.FullName).ToList();
                return isEs
                    ? $"Los inquilinos son: {string.Join(", ", names)}."
                    : $"The tenants are: {string.Join(", ", names)}.";
            }

            if (resource == SemanticQueryResource.Contracts)
            {
                var lines = items.Cast<SemanticContractResult>()
                    .Select(c => $"{c.TenantName} (Hab: {c.RoomName}, Fin: {(c.EffectiveEndDate.HasValue ? c.EffectiveEndDate.Value.ToString("yyyy-MM-dd") : "N/A")})");
                return isEs
                    ? $"Contratos:\n- {string.Join("\n- ", lines)}"
                    : $"Contracts:\n- {string.Join("\n- ", lines)}";
            }

            if (resource == SemanticQueryResource.Payments)
            {
                var lines = items.Cast<SemanticPaymentResult>()
                    .Select(p => $"{p.TenantName} ({p.Month}/{p.Year}): expected {p.ExpectedAmount:N2} €, paid {p.PaidAmount:N2} € [{p.Status}]");
                return isEs
                    ? $"Pagos:\n- {string.Join("\n- ", lines)}"
                    : $"Payments:\n- {string.Join("\n- ", lines)}";
            }

            if (resource == SemanticQueryResource.Expenses)
            {
                var lines = items.Cast<SemanticExpenseResult>()
                    .Select(e => $"{e.Category}: {e.Amount:N2} € ({e.Date:MM/yyyy})");
                return isEs
                    ? $"Gastos:\n- {string.Join("\n- ", lines)}"
                    : $"Expenses:\n- {string.Join("\n- ", lines)}";
            }
        }
        return result.ToString() ?? "";
    }

    private static string GetResourceSingularEs(SemanticQueryResource res) => res switch
    {
        SemanticQueryResource.Rooms => "habitación",
        SemanticQueryResource.Tenants => "inquilino",
        SemanticQueryResource.Contracts => "contrato",
        SemanticQueryResource.Payments => "pago",
        SemanticQueryResource.Expenses => "gasto",
        _ => "registro"
    };

    private static string GetResourcePluralEs(SemanticQueryResource res) => res switch
    {
        SemanticQueryResource.Rooms => "habitaciones",
        SemanticQueryResource.Tenants => "inquilinos",
        SemanticQueryResource.Contracts => "contratos",
        SemanticQueryResource.Payments => "pagos",
        SemanticQueryResource.Expenses => "gastos",
        _ => "registros"
    };

    private static string GetResourceSingularEn(SemanticQueryResource res) => res switch
    {
        SemanticQueryResource.Rooms => "room",
        SemanticQueryResource.Tenants => "tenant",
        SemanticQueryResource.Contracts => "contract",
        SemanticQueryResource.Payments => "payment",
        SemanticQueryResource.Expenses => "expense",
        _ => "record"
    };

    private static string GetResourcePluralEn(SemanticQueryResource res) => res switch
    {
        SemanticQueryResource.Rooms => "rooms",
        SemanticQueryResource.Tenants => "tenants",
        SemanticQueryResource.Contracts => "contracts",
        SemanticQueryResource.Payments => "payments",
        SemanticQueryResource.Expenses => "expenses",
        _ => "records"
    };

    public static string FormatValidationError(SemanticValidationResult result, string language)
    {
        bool isEs = language.Equals("es", StringComparison.OrdinalIgnoreCase);
        if (isEs)
        {
            return result.ErrorMessage switch
            {
                "low confidence" => "Lo siento, no tengo la suficiente confianza para responder a esa pregunta.",
                "limit exceeded" => "La consulta excede el límite máximo de resultados permitidos.",
                "unknown resource" => "Lo siento, solo puedo responder preguntas sobre habitaciones, inquilinos, contratos, pagos y gastos.",
                "unsupported operation" => "Esa operación no está soportada para el recurso solicitado.",
                "unknown field" => "La pregunta hace referencia a un campo que no está en el catálogo.",
                "unsupported operator" => "El operador utilizado no es compatible con el campo.",
                "invalid value" => "El valor de filtro proporcionado no es válido para ese campo.",
                "propertyId filter is not allowed" => "No está permitido filtrar por propertyId manualmente.",
                _ => $"Lo siento, la consulta no es válida: {result.ErrorMessage}"
            };
        }
        else
        {
            return result.ErrorMessage switch
            {
                "low confidence" => "I'm sorry, I don't have enough confidence to answer that question.",
                "limit exceeded" => "The query exceeds the maximum allowed result limit.",
                "unknown resource" => "I'm sorry, I can only answer questions about rooms, tenants, contracts, payments, and expenses.",
                "unsupported operation" => "That operation is not supported for the requested resource.",
                "unknown field" => "The question refers to a field not present in the catalog.",
                "unsupported operator" => "The operator used is not compatible with the field.",
                "invalid value" => "The filter value provided is invalid for that field.",
                "propertyId filter is not allowed" => "Filtering by propertyId manually is not allowed.",
                _ => $"I'm sorry, the query is invalid: {result.ErrorMessage}"
            };
        }
    }
}
