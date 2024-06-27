using System.Collections.Generic;

namespace ModelEINP.Model;

public class WolfPack
{
    public readonly List<Wolf> Members;
    private int Id { get; }
    public Wolf Father { get; set;}
    public Wolf Mother { get; set; }

    internal WolfPack(int packId, Wolf father, Wolf mother, List<Wolf> other)
    {
        Id = packId;
        Father = father;
        Mother = mother;
        Members = other ?? new List<Wolf>();
    }

    public void InsertNewborns(List<Wolf> newborns)
    {
        foreach (var wolf in newborns)
        {
            Members.Add(wolf);
        }
        
    }

    public int GetId()
    {
        return Id;
    }
    
}