using System.Collections.Generic;
using System.Linq;

namespace ModelEINP.Model;

public class PackManager
{
    private int _nextId = 1;
    private List<WolfPack> _packs = new List<WolfPack>();

    public int NextId()
    {
        return _nextId++;
    }

    public WolfPack CreateWolfPack(int packId, Wolf father, Wolf mother, List<Wolf> other)
    {
        var wolfPack = new WolfPack(packId, father, mother, other);
        _packs.Add(wolfPack);
        return wolfPack;
    }

    public WolfPack CreateWolfPack(Wolf loneWolf)
    {
        int packId = NextId();
        var wolfPack = new WolfPack(packId, loneWolf, null, new List<Wolf>());
        _packs.Add(wolfPack);
        return wolfPack;
    }

    public WolfPack FindById(int packId)
    {
        return _packs.FirstOrDefault(p => p.GetId() == packId);
    }
}
