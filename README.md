# Elk Island National Park Model

The EINP Model is a simple [MARS](https://www.mars-group.org/docs/tutorial/intro#what-is-mars) model that simmulates the bison, elk, moose and wolf populations of Elk Island National Park, including the predation of the other animals by the wolfs.

## Quickstart
To use the model clone the repository by running:
```bash
git clone https://github.com/paul-hinz/model-einp-prv.git
```

Then go into the newly downloaded project and run the simmulation:
```bash
cd einp-model/ModelEINP/
dotnet run -sm config.json
```

This will generate four files (`Bison_trips.geojson`, `Elk_trips.geojson`,	`Moose_trips.geojson`, `Wolf_trips.geojson`) with the simmulated coordinates of the animals.

### Viewing the output
The generated movement trajectories can be visualized by going to [kepler.gl](https://kepler.gl/demo) and uploading the generated files mentioned above.

## Configuration
In the subfolder _ModelEINP_ the file _config.json_ can be found, which offers lots of configuration possibilities. 
Most importantly the *Time Frame* and *Tick Length*, which could strongly change the simulations behaviour.

Each Animal Type has a list of parameters, which influence the simulated behaviours. For example running speeds and food requirements, but also many more. 
Each parameter is named to explain its meaning. The values for weights are always in kilograms while distances are in meters (and speeds therefore in m/s).

The output type is set to "trips" for the visualization using kepler.gl, but this can be changed in the config file as well to gain other informations. For me see the [MARS-Wiki](https://www.mars-group.org/docfx/index.html).
