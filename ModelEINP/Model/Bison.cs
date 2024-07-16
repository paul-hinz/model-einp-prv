using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace ModelEINP.Model;

public class Bison : AbstractAnimal {

    [ActiveConstructor]
    public Bison() {
    }
    
    [ActiveConstructor]
    public Bison(
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
        position) { 
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
    [PropertyDescription (Name="isLeading")]
    public override bool IsLeading { get; set; }
    [PropertyDescription (Name="herdId")] 
    public override int HerdId { get; set; }
    
    //Chance for a female animal to become pregnant per year
    public int ChanceForPregnancy = 10;


    
    protected string BisonType;
        
    #endregion
    
    #region Constants
    private readonly Dictionary<AnimalLifePeriod, double> _starvationRate = new()
    {
        /*
         * DailyFood / 16 is the gross starvation rate
         * but MaxSatiety = 100 != total food need per day
         * so the rate has to be adjusted
         * Total food need per day : 100 = (Total food need per day / 24) : adjusted rate
         */
        { AnimalLifePeriod.Calf, MaxSatiety * DailyFoodCalf / 16 / DailyFoodAdult }, 
        { AnimalLifePeriod.Adolescent, MaxSatiety * DailyFoodAdolescent / 16 / DailyFoodAdult }, 
        { AnimalLifePeriod.Adult, MaxSatiety * DailyFoodAdult / 16 / DailyFoodAdult }  
    };
    
    private readonly Dictionary<AnimalLifePeriod, double> _dehydrationRate =
        new()
        {
            /*
             * DailyWater / 24 is the gross starvation rate
             * but MaxHydration = 100 != total food need per day
             * so the rate has to be adjusted
             * Total water need per day : 100 = (Total water need per day / 24) : adjusted rate
             */
            { AnimalLifePeriod.Calf, MaxHydration * DailyWaterCalf / 24 / DailyWaterAdult },
            { AnimalLifePeriod.Adolescent, MaxHydration * DailyWaterAdolescent / 24 / DailyWaterAdult}, 
            { AnimalLifePeriod.Adult, MaxHydration * DailyWaterAdult / 24 / DailyWaterAdult}
        };
    
    [PropertyDescription]
    public static double DailyFoodAdult { get; set; }
    [PropertyDescription]
    public static double DailyFoodCalf { get; set; } 
    [PropertyDescription]
    public static double DailyFoodAdolescent { get; set; }
    
    //total need of water per day in liters   
    [PropertyDescription]
    public static double DailyWaterAdult { get; set; }
    [PropertyDescription]
    public static double DailyWaterCalf { get; set; }
    [PropertyDescription]
    public static double DailyWaterAdolescent { get; set; }
    [PropertyDescription] 
    public static double RunningSpeedInMs { get; set; }
    #endregion
    
    public override void Tick() { 
        
        if (IsFirstTick)
        {
            FirstTick();
            IsFirstTick = false;
            return;
        }
        
        //TODO: Dependent on TickLength
        TicksLived++;
        if (Pregnant) {
            if (TicksToDays(PregnancyDurationInTicks) < 80) {
                PregnancyDurationInTicks++;
            }
            else {
                PregnancyDurationInTicks = 0;
                LandscapeLayer.SpawnBison(LandscapeLayer, Perimeter, VegetationLayer, VectorWaterLayer, RasterWaterLayer,
                    AnimalType.BisonCalf, false, HerdId, Latitude, Longitude, Position);
            }
        }
        if (TicksToDays(TicksLived) >= 365)
        {
            YearlyRoutine();
        }
        if (!IsAlive) return;
       
        if (Satiety < 40) {
            LookForFoodAndEat();
        }
        else if (Hydration < 40) {
            LookForWaterAndDrink();
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
            _dehydrationRate[key] = (_dehydrationRate[key] / 3600) * TickLengthInSec;
        }
    }
    
    protected override void UpdateState()
    {
        int currentHour;
        if (LandscapeLayer.Context.CurrentTimePoint != null)
            currentHour = LandscapeLayer.Context.CurrentTimePoint.Value.Hour;
        else
            throw new NullReferenceException();
        

        if (currentHour is >= 21 and <= 23 || currentHour is >= 0 and <= 4 ) {   
            BurnSatiety(_starvationRate[LifePeriod] / 4); //less food is consumed while sleeping
            Dehydrate(_dehydrationRate[LifePeriod]/2);           //less water is consumed at night
        }
        else
        {
            BurnSatiety(_starvationRate[LifePeriod]);
            Dehydrate(_dehydrationRate[LifePeriod]);
        }
    }
    
    public override void YearlyRoutine() {
        TicksLived = 0;
        Age++;

        //decide sex once the bison reaches the adult stage
        var newLifePeriod = GetAnimalLifePeriodFromAge(Age);
        if (newLifePeriod != LifePeriod) {
            if (newLifePeriod == AnimalLifePeriod.Adult) {
                //50:50 chance of being male or female
                if (Random.Next(2) == 0)
                    AnimalType = AnimalType.BisonBull;
                else
                    AnimalType = AnimalType.BisonCow;
            }
            LifePeriod = newLifePeriod;
        }
        
        //max age 25
        if (Age > 15)
        {
            ChanceOfDeath = (Age - 15) * 10;
            var rnd = Random.Next(0, 100);
            if (rnd >= ChanceOfDeath) return;
            Die(MattersOfDeath.Age);
            return;
        }

        //check for possible reproduction
        if (!(Age >= ReproductionYears[0] && Age <= ReproductionYears[1])) return;

        if (!AnimalType.Equals(AnimalType.BisonCow)) return;

        if (LifePeriod == AnimalLifePeriod.Adult && Random.Next(100) < ChanceForPregnancy-1) {
            Pregnant = true;
        }
    }
    
    public override AnimalLifePeriod GetAnimalLifePeriodFromAge(int age)
    {
        if (age < 1) return AnimalLifePeriod.Calf;
        return age <= 2 ? AnimalLifePeriod.Adolescent : AnimalLifePeriod.Adult;
    }
    
}
