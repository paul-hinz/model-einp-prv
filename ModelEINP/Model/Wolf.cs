using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace ModelEINP.Model; 

public class Wolf : AbstractAnimal
{
    [ActiveConstructor]
    public Wolf() {
        //SetTestingValues();
        InitPack();
    }

    private const bool Logger = true;
    private static PackManager _packManager;

    
    [ActiveConstructor] 
    public Wolf(
        LandscapeLayer landscapeLayer, 
        Perimeter perimeter,
        VegetationLayer vegetationLayer,
        VectorWaterLayer waterLayer,
        RasterWaterLayer rasterWaterLayer,
        Guid id,
        AnimalType animalType,
        bool isLeading,
        int herdId,
        double latitude, 
        double longitude,
        Position position) : 
        base(landscapeLayer, 
            perimeter,
            vegetationLayer,
            waterLayer,
            rasterWaterLayer,
            id,
            animalType,
            isLeading,
            herdId,
            latitude, 
            longitude,
            position)
    {
        SetTestingValues();
        InitPack();
    }

    private void SetTestingValues()
    {
        IsLeading = true;
        HerdId = 1;
        Age = 3;
        _animalType = _random.Next(2) == 0 ? AnimalType.WolfMale : AnimalType.WolfFemale;
    }

    #region Properties and Fields
    
    public override double Hydration { get; set; } = MaxHydration;
    public override double Satiety { get; set; } = MaxSatiety;
    public override Position Position { get; set; }
    public override Position Target { get; set; }
    [PropertyDescription(Name = "Latitude")]
    public override double Latitude { get; set; }
    [PropertyDescription(Name = "Longitude")]
    public override double Longitude { get; set; }
    
    [PropertyDescription(Name = "IsLeading")]
    protected override bool IsLeading { get; set; }
    [PropertyDescription (Name="HerdId")]
    protected override int HerdId { get; set; }
    
    #endregion

    #region Constants
    private readonly Dictionary<AnimalLifePeriod, double> _starvationRate = new()
    {
        /*
         * ToDo: Needs to be adjusted with actual data
         * DummyValue: A Wolf can survive with eating every 3 days
         * --> Verliert jede Stunde ein 3*18 tel, weil durch die Nacht in Summe 18 Ticks pro Tag abgezogen werden
         */
        { AnimalLifePeriod.Calf, MaxSatiety / (3 * 18) }, 
        { AnimalLifePeriod.Adolescent, MaxSatiety / (3 * 18) }, 
        { AnimalLifePeriod.Adult, MaxSatiety / (3 * 18) }  
    };
    
    //total need of food per day in kilograms
    [PropertyDescription]
    public static double DailyFoodAdult { get; set; }
    [PropertyDescription]
    public static double DailyFoodPup { get; set; } 
    [PropertyDescription]
    public static double DailyFoodAdolescent { get; set; }
    
    //simulation / behavior parameters
    [PropertyDescription]
    public static double HungryThreshold { get; set; }
    [PropertyDescription]
    public static double MaxHuntDistanceInM { get; set; }
    [PropertyDescription]
    public static int PregnancyDurationInTicks { get; set; }

    //Chance for an adult female animal to become pregnant per year in percent
    //ToDo: adjust rate
    private const int ChanceForPregnancy = 99;
    
    #endregion

    //Lots of TODO
    public override void Tick()
    {
        _hoursLived++;
        
        //give birth
        if (_hoursLived % 1 == 0 && _pregnant) {
            if (_pregnancyDuration < PregnancyDurationInTicks) {
                if(Logger) Console.WriteLine("Wolf: " + ID + "is pregnant");
                _pregnancyDuration++;
            }
            else {
                _pregnancyDuration = 0;
                var list = new List<Wolf>();
                for (var i = 0; i < 4; i++)
                {
                    var wolf = _landscapeLayer.SpawnWolf(_landscapeLayer, _perimeter, _vegetationLayer, _vectorWaterLayer, _rasterWaterLayer,
                        AnimalType.WolfNewborn, false, HerdId, Latitude, Longitude, Position);
                    list.Add(wolf);
                }
                _packManager.FindById(HerdId).InsertNewborns(list);
                if(Logger) Console.WriteLine("Wolf: " + ID + "gave birth");
            }
        }
        
        if (_hoursLived == 2)
        {
            YearlyRoutine();
        }
        if (!IsAlive) return;
       
        if (Satiety < HungryThreshold) {
            LookForPreyAndHunt();
        }
        
        else {
            DoRandomWalk(10);
        }
        UpdateState();
        
    }

    protected override void UpdateState()
    {
        
        int currentHour;
        if (_landscapeLayer.Context.CurrentTimePoint != null)
            currentHour = _landscapeLayer.Context.CurrentTimePoint.Value.Hour;
        else
            throw new NullReferenceException();
        
        // adjust satiety
        if (currentHour is >= 21 and <= 23 or >= 0 and <= 4 ) {   
            BurnSatiety(_starvationRate[_LifePeriod] / 4); //less food is consumed while sleeping
        }
        else
        {
            BurnSatiety(_starvationRate[_LifePeriod]);
        }
        if (Satiety > 100.0) Satiety = 100;
        
        //starve
        if (Satiety == 0.0)
        {
            Die(MattersOfDeath.NoFood);
            if(Logger) Console.WriteLine("A Wolf starved");
        }
    }

    public override void YearlyRoutine()
    {
        _hoursLived = 0;
        Age++;

        //new LifePeriod
        var newLifePeriod = GetAnimalLifePeriodFromAge(Age);
        if (newLifePeriod != _LifePeriod)
        {
            if (newLifePeriod == AnimalLifePeriod.Adolescent) _animalType = AnimalType.WolfPup;
            
            if (newLifePeriod == AnimalLifePeriod.Adult)
            {
                //decide sex, 50:50 chance of being male or female
                _animalType = _random.Next(2) == 0 ? AnimalType.WolfMale : AnimalType.WolfFemale;
                if(Logger) Console.WriteLine("Wolf: " + ID + "grew up and is: " + _animalType + " and isLeading: " + IsLeading);

                //todo: leave pack
            }
            _LifePeriod = newLifePeriod;
        }
        
        //reproduction
        if (!(Age >= _reproductionYears[0] && Age <= _reproductionYears[1])) return;
        
        //only mother wolf can get pregnant when a father is present, todo: check for distance
        if (!_animalType.Equals(AnimalType.WolfFemale) || !IsLeading) return;
        var pack = _packManager.FindById(HerdId);
        if(Logger) Console.WriteLine("Wolf: " + _animalType + " HerdId: " + HerdId + " is leading: "+ IsLeading);

        if (pack?.Father is not null
            && _LifePeriod == AnimalLifePeriod.Adult
            && _random.Next(100) < ChanceForPregnancy-1) {
            if(Logger) Console.WriteLine("Wolf: " + ID + "got pregnant");
            _pregnant = true;
        }
        
    }

    public override AnimalLifePeriod GetAnimalLifePeriodFromAge(int age)
    {
        if (age < 1) return AnimalLifePeriod.Calf;
        return age <= 2 ? AnimalLifePeriod.Adolescent : AnimalLifePeriod.Adult;
    }
    
    private void LookForPreyAndHunt()
    {
        if(Logger) Console.WriteLine("Looking for prey");
        //var testTargets = _landscapeLayer.Environment.Explore(Position);
        //Console.WriteLine(testTargets.Count());
        var target = _landscapeLayer.Environment.GetNearest(Position, MaxHuntDistanceInM, IsAnimalPrey);
        
        //ToDo: create calculation for different amount of satiety, depending on prey and size of hunting group(pack)
        if (target is null) return;
        Satiety += 50;
        //ToDo: Thread safety -> two wolves cant eat the same prey in the same tick
        target.Die(MattersOfDeath.Culling);
        if(Logger) Console.WriteLine("Found prey: " + target.ID + "  and eaten");
    }

    //checks if animal is possible prey
    private static bool IsAnimalPrey(AbstractAnimal animal)
    {
        return animal._animalType switch
        {
            AnimalType.BisonCow 
                or AnimalType.BisonBull 
                or AnimalType.BisonCalf 
                or AnimalType.BisonNewborn
                or AnimalType.ElkCow 
                or AnimalType.ElkBull 
                or AnimalType.ElkCalf 
                or AnimalType.ElkNewborn
                or AnimalType.MooseCow 
                or AnimalType.MooseBull 
                or AnimalType.MooseCalf
                or AnimalType.MooseNewborn 
                => true,
            
            _ => false
        };
    }
    
    private void InitPack()
    {
        _packManager ??= new PackManager();
        var pack = _packManager.FindById(HerdId);
        if (pack is null)
        {
            var members = new List<Wolf>();
            members.Add(this);
            pack = _packManager.CreateWolfPack(HerdId, null, null, members);
        }
        else
        {
            pack.Members.Add(this);
        }

        if (IsLeading)
        {
            if(_animalType == AnimalType.WolfMale)
            {
                pack.Father = this;
            }
            if(_animalType == AnimalType.WolfFemale)
            {
                pack.Mother = this;
            }
        }
    }
}