using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace ModelEINP.Model; 

public class Wolf : AbstractAnimal
{
    [ActiveConstructor]
    public Wolf() {
    }
    
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
            position) { 
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
    //Chance for a female animal to become pregnant per year
    public int ChanceForPregnancy = 0;


    protected string MooseType;
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

    //total need of food per day in kilogramms
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
    #endregion

    public override void Tick()
    {
    }

    protected override void UpdateState()
    {
    }

    public override void YearlyRoutine()
    {
    }

    public override AnimalLifePeriod GetAnimalLifePeriodFromAge(int age)
    {
        if (age < 1) return AnimalLifePeriod.Calf;
        return age <= 2 ? AnimalLifePeriod.Adolescent : AnimalLifePeriod.Adult;
    }
    
}