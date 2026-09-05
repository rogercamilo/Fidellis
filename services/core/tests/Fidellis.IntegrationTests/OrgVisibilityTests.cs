using Fidellis.Modules.Donations;
using Xunit;

namespace Fidellis.IntegrationTests;

public class OrgVisibilityTests
{
    private static readonly Guid Rede = Guid.NewGuid();     // raiz (diocese)
    private static readonly Guid ParoquiaA = Guid.NewGuid();
    private static readonly Guid FilialA1 = Guid.NewGuid();  // filial de A
    private static readonly Guid ParoquiaB = Guid.NewGuid();
    private static readonly Guid Outra = Guid.NewGuid();     // rede não relacionada

    private static readonly (Guid, Guid?)[] Tree =
    [
        (Rede, null),
        (ParoquiaA, Rede),
        (FilialA1, ParoquiaA),
        (ParoquiaB, Rede),
        (Outra, null),
    ];

    [Fact]
    public void Member_of_unit_sees_unit_and_its_branches()
    {
        var visible = OrgVisibility.VisibleOrgIds([ParoquiaA], Tree);
        Assert.Equal(new HashSet<Guid> { ParoquiaA, FilialA1 }, visible);
    }

    [Fact]
    public void Member_of_network_root_sees_whole_subtree_only()
    {
        var visible = OrgVisibility.VisibleOrgIds([Rede], Tree);
        Assert.Equal(new HashSet<Guid> { Rede, ParoquiaA, FilialA1, ParoquiaB }, visible);
        Assert.DoesNotContain(Outra, visible);
    }

    [Fact]
    public void Member_of_leaf_sees_only_itself()
    {
        var visible = OrgVisibility.VisibleOrgIds([FilialA1], Tree);
        Assert.Equal(new HashSet<Guid> { FilialA1 }, visible);
    }
}
