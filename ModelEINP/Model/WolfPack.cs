using System;
using System.Collections.Generic;
using log4net.Repository.Hierarchy;

namespace ModelEINP.Model;

public class WolfPack
{
    public readonly List<Wolf> Members;
    private int Id { get; }
    public Wolf Father { get; set;}
    public Wolf Mother { get; set; }

    public WolfEncirclingList EncirclingList;

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
    
    public Wolf GetLeader()
    {
        if (Father is null) return Mother;
        return Father;
    }

    //sets hunting status for whole pack
    public void StartHunt(AbstractAnimal target)
    {
        foreach (var wolf in Members)
        {
            wolf.IsPartOfHunt = true;
            wolf.HuntingTarget = target;
        }

        EncirclingList = new WolfEncirclingList();
    }
    
    //removes hunting fields for whole pack
    public void EndHunt()
    {
        foreach (var wolf in Members)
        {
            wolf.IsPartOfHunt = false;
            wolf.HuntingTarget = null;
        }

        EncirclingList = null;
    }

    //deletes a wolf from the pack and fills gap in breeding pair, if needed and possible
    public void LeavePack(Wolf wolf)
    {
        if (Father == wolf)
        {
            Father = Members.Find(wolf1 => wolf1.AnimalType == AnimalType.WolfMale);
            if (Father is not null) Father.IsLeading = true;
        }
        else if (Mother == wolf)
        {
            Mother = Members.Find(wolf1 => wolf1.AnimalType == AnimalType.WolfFemale);
            if (Mother is not null) Mother.IsLeading = true;
        }

        Members.Remove(wolf);
    }

    public void ShareKill(AbstractAnimal prey)
    {
        var food = prey.SatietyFactor();
        var singleFood = food / Members.Count;
        foreach (var wolf in Members)
        {
            wolf.GiveFood(singleFood);
        }
    }
    
}