using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace ModelEINP.Model; 

public class Wolf : AbstractAnimal
{
    [ActiveConstructor]
    public Wolf()
    {
    }

    private void TestHerd()
    {
        Console.WriteLine(HerdId);
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
        AnimalType = Random.Next(2) == 0 ? AnimalType.WolfMale : AnimalType.WolfFemale;
    }

    #region Properties and Fields
    
    protected override double Hydration { get; set; } = MaxHydration;
    protected override double Satiety { get; set; } = MaxSatiety;
    public override Position Position { get; set; }
    public override Position Target { get; set; }
    [PropertyDescription(Name = "Latitude")]
    public override double Latitude { get; set; }
    [PropertyDescription(Name = "Longitude")]
    public override double Longitude { get; set; }
    
    [PropertyDescription(Name = "IsLeading")]
    public override bool IsLeading { get; set; }
    [PropertyDescription (Name="HerdId")] 
    public override int HerdId { get; set; }
    
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
    public static int PregnancyDurationInDays { get; set; }

    [PropertyDescription] 
    public static double RunningSpeedInMs { get; set; }

    //Chance for an adult female animal to become pregnant per year in percent
    //ToDo: adjust rate
    private const int ChanceForPregnancy = 99;
    
    #endregion
    
    public override void Tick()
    {

        if (IsFirstTick)
        {
            FirstTick();
            IsFirstTick = false;
            return;
        }

        UpdateDaysLived();
        
        //check for pregnancy duration and eventually give birth
        if (Pregnant) {
            if (TicksToDays(PregnancyDurationInTicks) < PregnancyDurationInDays) {
                if(Logger) Console.WriteLine("Wolf: " + ID + "is pregnant");
                PregnancyDurationInTicks++;
            }
            else {
                PregnancyDurationInTicks = 0;
                var list = new List<Wolf>();
                for (var i = 0; i < 4; i++)
                {
                    var wolf = LandscapeLayer.SpawnWolf(LandscapeLayer, Perimeter, VegetationLayer, VectorWaterLayer, RasterWaterLayer,
                        AnimalType.WolfNewborn, false, HerdId, Latitude, Longitude, Position);
                    list.Add(wolf);
                }
                _packManager.FindById(HerdId).InsertNewborns(list);
                if(Logger) Console.WriteLine("Wolf: " + ID + "gave birth");
            }
        }
        
        //check for yearly event
        if (TicksToDays(TicksLived) >= 365)
        {
            YearlyRoutine();
        }
        if (!IsAlive) return;
       
        //daily life
        if (Satiety < HungryThreshold) {
            LookForPreyAndHunt();
        }
        
        else {
            DoRandomWalk(10);
        }
        
        UpdateState();
        
    }

    public override void FirstTick()
    {
        if (LandscapeLayer.Context.StartTimePoint is not null) 
            LastDate = LandscapeLayer.Context.StartTimePoint.Value.Date;
        CalculateParams();
        InitPack();
    }

    public override void CalculateParams()
    {
        //Calculating Movements per Tick
        RunDistancePerTick = RunningSpeedInMs * TickLengthInSec;
        RandomWalkMaxDistanceInM = (int)Math.Round ((RandomWalkMaxDistanceInM / (double) 3600) * TickLengthInSec);
        RandomWalkMinDistanceInM = (int)Math.Round ((RandomWalkMinDistanceInM / (double) 3600) * TickLengthInSec);
        
        // Recalculation of dehydration and starvation rates 
        var keys = _starvationRate.Keys.ToList();
        foreach (var key in keys)
        {
            _starvationRate[key] = (_starvationRate[key] / 3600) * TickLengthInSec;
        }
    }

    protected override void UpdateState()
    {
        
        int currentHour;
        if (LandscapeLayer.Context.CurrentTimePoint != null)
            currentHour = LandscapeLayer.Context.CurrentTimePoint.Value.Hour;
        else
            throw new NullReferenceException();
        
        // adjust satiety
        if (currentHour is >= 21 and <= 23 or >= 0 and <= 4 ) {   
            BurnSatiety(_starvationRate[LifePeriod] / 4); //less food is consumed while sleeping
        }
        else
        {
            BurnSatiety(_starvationRate[LifePeriod]);
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
        TicksLived = 0;
        Age++;

        //new LifePeriod
        var newLifePeriod = GetAnimalLifePeriodFromAge(Age);
        if (newLifePeriod != LifePeriod)
        {
            if (newLifePeriod == AnimalLifePeriod.Adolescent) AnimalType = AnimalType.WolfPup;
            
            if (newLifePeriod == AnimalLifePeriod.Adult)
            {
                //decide sex, 50:50 chance of being male or female
                AnimalType = Random.Next(2) == 0 ? AnimalType.WolfMale : AnimalType.WolfFemale;
                if(Logger) Console.WriteLine("Wolf: " + ID + "grew up and is: " + AnimalType + " and isLeading: " + IsLeading);

            }
            LifePeriod = newLifePeriod;
        }
        
        //reproduction
        if (!(Age >= ReproductionYears[0] && Age <= ReproductionYears[1])) return;
        
        //only mother wolf can get pregnant when a father is present, todo: check for distance
        if (!AnimalType.Equals(AnimalType.WolfFemale) || !IsLeading) return;
        var pack = _packManager.FindById(HerdId);
        if(Logger) Console.WriteLine("Wolf: " + AnimalType + " HerdId: " + HerdId + " is leading: "+ IsLeading);

        if (pack?.Father is not null
            && LifePeriod == AnimalLifePeriod.Adult
            && Random.Next(100) < ChanceForPregnancy-1) {
            if(Logger) Console.WriteLine("Wolf: " + ID + "got pregnant");
            Pregnant = true;
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
        var target = LandscapeLayer.Environment.GetNearest(Position, MaxHuntDistanceInM, IsAnimalPrey);
        
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
        return animal.AnimalType switch
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
            if(AnimalType == AnimalType.WolfMale)
            {
                pack.Father = this;
            }
            if(AnimalType == AnimalType.WolfFemale)
            {
                pack.Mother = this;
            }
        }
    }
}