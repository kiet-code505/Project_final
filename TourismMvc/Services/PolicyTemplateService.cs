using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class PolicyTemplateService(
    AppDbContext db,
    ILogger<PolicyTemplateService> logger) : IPolicyTemplateService
{
    public async Task<PolicyIndexViewModel> GetPagedPoliciesAsync(string? search, int? contactId, int page, int pageSize)
    {
        if (pageSize <= 0) pageSize = 10;
        var q = db.Policies
            .Include(p => p.Contact)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.TemplateName, pattern)
                          || (p.Contact != null && EF.Functions.ILike(p.Contact.BranchName, pattern)));
        }

        if (contactId.HasValue && contactId.Value > 0)
        {
            q = q.Where(p => p.ContactId == contactId.Value);
        }

        q = q.OrderByDescending(p => p.UpdatedAt == null)
             .ThenByDescending(p => p.UpdatedAt)
             .ThenByDescending(p => p.PolicyTemplateID);

        var total = await q.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var policies = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var policyIds = policies.Select(p => p.PolicyTemplateID).ToList();
        var usageCounts = await db.Departures
            .Where(d => policyIds.Contains(d.PolicyTemplateId))
            .GroupBy(d => d.PolicyTemplateId)
            .Select(g => new { PolicyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PolicyId, x => x.Count);

        var allBranches = await db.Branches.AsNoTracking().OrderBy(b => b.BranchName).ToListAsync();

        var totalUsage = await db.Departures.CountAsync();
        var allPolicies = await db.Policies.AsNoTracking().ToListAsync();
        var linkedCount = allPolicies.Count(p => p.ContactId.HasValue);
        var unusedCount = allPolicies.Count(p => !db.Departures.Any(d => d.PolicyTemplateId == p.PolicyTemplateID));

        return new PolicyIndexViewModel
        {
            Items = policies.Select(p => new PolicyListItem
            {
                Policy = p,
                BranchName = p.Contact?.BranchName,
                UsageCount = usageCounts.GetValueOrDefault(p.PolicyTemplateID)
            }).ToList(),
            Search = search,
            ContactId = contactId,
            Branches = allBranches,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            StatTotal = allPolicies.Count,
            StatLinkedBranch = linkedCount,
            StatTotalUsage = totalUsage,
            StatUnused = unusedCount
        };
    }

    public async Task<PolicyFormViewModel?> GetForEditAsync(int id)
    {
        var p = await db.Policies.AsNoTracking().FirstOrDefaultAsync(x => x.PolicyTemplateID == id);
        if (p == null) return null;

        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.BranchName).ToListAsync();

        return new PolicyFormViewModel
        {
            PolicyTemplateID = p.PolicyTemplateID,
            TemplateName = p.TemplateName,
            ContactId = p.ContactId,
            TourPriceIncludes = p.TourPriceIncludes,
            TourPriceExcludes = p.TourPriceExcludes,
            PaymentConditions = p.PaymentConditions,
            CancellationRules = p.CancellationRules,
            AvailableBranches = branches
        };
    }

    public async Task<bool> CreateAsync(PolicyFormViewModel vm, ModelStateDictionary modelState)
    {
        Normalize(vm);
        await ValidateUniqueAsync(vm, modelState);
        if (!modelState.IsValid)
        {
            vm.AvailableBranches = await db.Branches.AsNoTracking().OrderBy(b => b.BranchName).ToListAsync();
            return false;
        }

        var policy = new PolicyTemplate
        {
            TemplateName = vm.TemplateName,
            ContactId = vm.ContactId,
            TourPriceIncludes = vm.TourPriceIncludes,
            TourPriceExcludes = vm.TourPriceExcludes,
            PaymentConditions = vm.PaymentConditions,
            CancellationRules = vm.CancellationRules
        };

        db.Policies.Add(policy);
        await db.SaveChangesAsync();
        logger.LogInformation("PolicyTemplateService: Created policy {Id} - {Name}", policy.PolicyTemplateID, policy.TemplateName);

        vm.PolicyTemplateID = policy.PolicyTemplateID;
        return true;
    }

    public async Task<bool> UpdateAsync(int id, PolicyFormViewModel vm, ModelStateDictionary modelState)
    {
        var policy = await db.Policies.FirstOrDefaultAsync(p => p.PolicyTemplateID == id);
        if (policy == null) return false;

        vm.PolicyTemplateID = id;
        Normalize(vm);
        await ValidateUniqueAsync(vm, modelState);
        if (!modelState.IsValid)
        {
            vm.AvailableBranches = await db.Branches.AsNoTracking().OrderBy(b => b.BranchName).ToListAsync();
            return false;
        }

        policy.TemplateName = vm.TemplateName;
        policy.ContactId = vm.ContactId;
        policy.TourPriceIncludes = vm.TourPriceIncludes;
        policy.TourPriceExcludes = vm.TourPriceExcludes;
        policy.PaymentConditions = vm.PaymentConditions;
        policy.CancellationRules = vm.CancellationRules;

        await db.SaveChangesAsync();
        logger.LogInformation("PolicyTemplateService: Updated policy {Id} - {Name}", policy.PolicyTemplateID, policy.TemplateName);
        return true;
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int id)
    {
        var policy = await db.Policies.FirstOrDefaultAsync(p => p.PolicyTemplateID == id);
        if (policy == null) return (false, "Không tìm thấy mẫu chính sách cần xóa.");

        var departureCount = await db.Departures.CountAsync(d => d.PolicyTemplateId == id);
        if (departureCount > 0)
        {
            return (false, $"Không thể xóa mẫu chính sách \"{policy.TemplateName}\" vì đang được áp dụng cho {departureCount} chuyến khởi hành.");
        }

        db.Policies.Remove(policy);
        await db.SaveChangesAsync();
        logger.LogInformation("PolicyTemplateService: Deleted policy {Id} - {Name}", policy.PolicyTemplateID, policy.TemplateName);
        return (true, null);
    }

    public async Task<List<PolicyTemplate>> GetAllPoliciesSimpleAsync()
    {
        return await db.Policies
            .Include(p => p.Contact)
            .AsNoTracking()
            .OrderBy(p => p.TemplateName)
            .ToListAsync();
    }

    private static void Normalize(PolicyFormViewModel vm)
    {
        vm.TemplateName = vm.TemplateName?.Trim() ?? "";
        vm.TourPriceIncludes = string.IsNullOrWhiteSpace(vm.TourPriceIncludes) ? null : vm.TourPriceIncludes.Trim();
        vm.TourPriceExcludes = string.IsNullOrWhiteSpace(vm.TourPriceExcludes) ? null : vm.TourPriceExcludes.Trim();
        vm.PaymentConditions = string.IsNullOrWhiteSpace(vm.PaymentConditions) ? null : vm.PaymentConditions.Trim();
        vm.CancellationRules = string.IsNullOrWhiteSpace(vm.CancellationRules) ? null : vm.CancellationRules.Trim();
    }

    private async Task ValidateUniqueAsync(PolicyFormViewModel vm, ModelStateDictionary modelState)
    {
        var id = vm.PolicyTemplateID ?? 0;
        if (await db.Policies.AnyAsync(p => p.PolicyTemplateID != id && p.TemplateName == vm.TemplateName))
        {
            modelState.AddModelError(nameof(vm.TemplateName), $"Mẫu chính sách \"{vm.TemplateName}\" đã tồn tại");
        }

        if (vm.ContactId.HasValue && !await db.Branches.AnyAsync(b => b.ContactID == vm.ContactId.Value))
        {
            modelState.AddModelError(nameof(vm.ContactId), "Chi nhánh được chọn không tồn tại");
        }
    }
}
