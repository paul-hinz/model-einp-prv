using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace ModelEINP.Model;

public class LandscapeLayer : AbstractLayer {
    
    #region Properties and Fields
    
    public GeoHashEnvironment<AbstractAnimal> Environment { get; set; }

    private List<Bison> Bisons { get; set; }
    private List<Moose> Moose { get; set; }
    private List<Elk> Elks { get; set; }
    private List<Wolf> Wolfs { get; set; }
    [PropertyDescription(Name = "Perimeter")]
    public Perimeter Fence { get; set; }

    private RegisterAgent _registerAgent;
    private UnregisterAgent _unregisterAgent;

    #endregion
    
    /// <summary>
    /// The LandscapeLayer registers the animals in the runtime system. In this way, the tick methods
    /// of the agents can be executed later. Then the expansion of the simulation area is calculated using
    /// the raster layers described in config.json. An environment is created with this bounding box.
    /// </summary>
    /// <param name="layerInitData"></param>
    /// <param name="registerAgentHandle"></param>
    /// <param name="unregisterAgentHandle"></param>
    /// <returns>true if the agents where registered</returns>
    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent unregisterAgentHandle) {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);
        _registerAgent = registerAgentHandle;
        _unregisterAgent = unregisterAgentHandle;

        // Calculate and expand extent
        var baseExtent = new Envelope(Fence.Extent.ToEnvelope());
        Console.WriteLine(new BoundingBox(baseExtent));

        // Create GeoHashEnvironment with the calculated extent
        Environment = GeoHashEnvironment<AbstractAnimal>.BuildByBBox(new BoundingBox(baseExtent), 1000);

        var agentManager = layerInitData.Container.Resolve<IAgentManager>();
        Bisons = agentManager.Spawn<Bison, LandscapeLayer>().ToList();
        Moose = agentManager.Spawn<Moose, LandscapeLayer>().ToList();
        Elks = agentManager.Spawn<Elk, LandscapeLayer>().ToList();
        Wolfs = agentManager.Spawn<Wolf, LandscapeLayer>().ToList();
        
        return Bisons.Count + Moose.Count + Elks.Count + Wolfs.Count > 0;
    }

    public void SpawnBison(LandscapeLayer landscapeLayer, Perimeter perimeter, 
        VegetationLayer vegetationLayer, VectorWaterLayer waterLayer, RasterWaterLayer rasterWaterLayer, AnimalType animalType, 
        bool isLeading, int herdId, double latitude, double longitude, Position position) {
        var newBison = new Bison(landscapeLayer, perimeter, vegetationLayer, waterLayer, rasterWaterLayer,
            Guid.NewGuid(), animalType, isLeading, herdId, latitude, longitude, position);
        Bisons.Add(newBison);
        Environment.Insert(newBison);
        _registerAgent(landscapeLayer, newBison);
    }
    
    public void SpawnElk(LandscapeLayer landscapeLayer, Perimeter perimeter, 
        VegetationLayer vegetationLayer, VectorWaterLayer waterLayer, RasterWaterLayer rasterWaterLayer, AnimalType animalType, 
        bool isLeading, int herdId, double latitude, double longitude, Position position) {
        var newElk = new Elk(landscapeLayer, perimeter, vegetationLayer, waterLayer, rasterWaterLayer,
            Guid.NewGuid(), animalType, isLeading, herdId, latitude, longitude, position);
        Elks.Add(newElk);
        Environment.Insert(newElk);
        _registerAgent(landscapeLayer, newElk);
    }
    
    public void SpawnMoose(LandscapeLayer landscapeLayer, Perimeter perimeter, 
        VegetationLayer vegetationLayer, VectorWaterLayer waterLayer, RasterWaterLayer rasterWaterLayer, AnimalType animalType, 
        bool isLeading, int herdId, double latitude, double longitude, Position position) {
        var newMoose = new Moose(landscapeLayer, perimeter, vegetationLayer, waterLayer, rasterWaterLayer,
            Guid.NewGuid(), animalType, isLeading, herdId, latitude, longitude, position);
        Moose.Add(newMoose);
        Environment.Insert(newMoose);
        _registerAgent(landscapeLayer, newMoose);
    }
    
    public Wolf SpawnWolf(LandscapeLayer landscapeLayer, Perimeter perimeter, 
        VegetationLayer vegetationLayer, VectorWaterLayer waterLayer, RasterWaterLayer rasterWaterLayer, AnimalType animalType, 
        bool isLeading, int herdId, double latitude, double longitude, Position position) {
        var newWolf = new Wolf(landscapeLayer, perimeter, vegetationLayer, waterLayer, rasterWaterLayer,
            Guid.NewGuid(), animalType, isLeading, herdId, latitude, longitude, position);
        Wolfs.Add(newWolf);
        Environment.Insert(newWolf);
        _registerAgent(landscapeLayer, newWolf);
        return newWolf;
    }

    public void RemoveAnimal(LandscapeLayer landscapeLayer, AbstractAnimal animal) {
        _unregisterAgent(landscapeLayer, animal);
        if (animal._animalType is AnimalType.BisonBull or AnimalType.BisonCalf 
            or AnimalType.BisonCow or AnimalType.BisonCow) {
            Bisons.Remove((Bison)animal);
        } 
        else if (animal._animalType is AnimalType.ElkCalf or AnimalType.ElkCow 
                 or AnimalType.ElkBull or AnimalType.ElkNewborn) {
            Elks.Remove((Elk)animal);
        }
        else if (animal._animalType is AnimalType.MooseCalf or AnimalType.MooseCow 
                 or AnimalType.MooseBull or AnimalType.MooseNewborn) {
            Moose.Remove((Moose)animal);
        }
        else if (animal._animalType is AnimalType.WolfFemale or AnimalType.WolfMale 
                 or AnimalType.WolfPup or AnimalType.WolfNewborn) {
            Wolfs.Remove((Wolf)animal);
        }
        Environment.Remove(animal);
    }
}
