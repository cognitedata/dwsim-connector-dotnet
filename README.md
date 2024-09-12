# DWSIM Cognite Data Fusion Connector .NET

DWSIMConnector is a connector that integrates DWSIM simulator with CDF (Cognite Data Fusion). Once installed at a host machine with access to DWSIM, the connector reads model files and routines defined in CDF simulators API and uses them to run DWSIM simulations on schedule. The simulation results are saved in CDF as simulation data or, optionally, as time series data points.

This connector uses COM (Component Object Model) to communicate with DWSIM. It extends the [simulator utils](https://github.com/cognitedata/dotnet-simulator-utils) by implementing specific logic for the COM interface, serving as a practical example for creating custom CDF connectors to other COM-compatible simulators.


# Running the connector

- Clone this repository
- Run `dotnet build`
- Copy the `config/config.example.yml` into the current working directory and rename it to `config.yml`
- Modify the `config.yml` file with your own Cognite Data Fusion credentials
- Run `dotnet run Service/Service.csproj`


# Building an installer

This repo comes with a setup creation template for the DWSIM connector. Here's how to use it:

- Change directory to the `Installer` folder
- Run `\build.ps1 -b msbuild -v ${{ VERSION_OF_DWSIM_CONNECTOR_HERE }} -d "DWSIM connector Installer" -c .\setup-config.json`

# About the code

Each simulator connector is comprised of the following 4 parts:

## The Connector Runtime

The connector runtime is a wrapper that runs the entire connector. It connects to CDF (Cognite Data Fusion), downloads model files, runs simulations, and handles the scheduling of simulations. To implement this in your own connector, you need to register your classes in the ConfigureServices function, define a ConnectorName, and you should be all set. An example for this can be seen in this repo [here](Connector/ConnectorRuntime.cs).

## The Connector configuration

The connector accepts configuration via a [config.yml](Connector/config/config.example.yml) file. This file contains CDF (Cognite Data Fusion) credentials, local state storage configuration and simulator specific configuration. Simulator specific configuration is nested under the `automation` tag. If you want to define any other variables to be read from that file, you must define them in the [AutomationConfig.cs](Connector/Dwsim/AutomationConfig.cs) file first. 

## The simulator client

The simulator client is the part which actually connects to the simulator itself and acts as a bridge between CDF and the simulator. It has methods which are used to get the Simulator version, connector version, run a simulation or extract simulator model information. 

[Source](Connector/Dwsim/DwsimClient.cs)

## The routine

This is used by the simulation runner to run a routine, it implements methods to set data into a simulation and get data from a simulation. 

[Source](Connector/Dwsim/DwsimRoutine.cs)


## License

Apache v2, see [LICENSE](./LICENSE).

## Contribution

To maintain the consistency and quality of the repo, please ensure any contributions adhere to the established structure and guidelines. Before submitting any additions or modifications, ensure that the code builds successfully.