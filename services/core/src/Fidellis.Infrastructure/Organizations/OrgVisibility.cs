namespace Fidellis.Infrastructure.Organizations;

/// <summary>
/// Regra de visibilidade Rede→Unidade: um usuário vinculado a uma organização enxerga essa
/// organização e todas as suas filiais (descendentes por <c>parent_id</c>). Função pura/testável,
/// compartilhada entre os módulos.
/// </summary>
public static class OrgVisibility
{
    public static HashSet<Guid> VisibleOrgIds(
        IEnumerable<Guid> memberOrgIds,
        IReadOnlyCollection<(Guid Id, Guid? ParentId)> allOrgs)
    {
        var childrenByParent = allOrgs
            .Where(o => o.ParentId is not null)
            .GroupBy(o => o.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var visible = new HashSet<Guid>();
        var queue = new Queue<Guid>(memberOrgIds);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visible.Add(id)) continue;
            if (childrenByParent.TryGetValue(id, out var kids))
                foreach (var k in kids) queue.Enqueue(k);
        }
        return visible;
    }
}
