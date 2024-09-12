/**
 * Copyright 2024 Cognite AS
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
 
using CogniteSdk.Alpha;
using System.Collections.Generic;

namespace Connector
{
    static class SimulatorDefinition
    {
        public static SimulatorCreate Get()
        {
            return new SimulatorCreate()
            {
                ExternalId = "DWSIM",
                Name = "DWSIM",
                FileExtensionTypes = new List<string> { "dwxmz" },
                ModelTypes = new List<SimulatorModelType> {
                        new SimulatorModelType {
                            Name = "Steady State",
                            Key = "SteadyState",
                        }
                    },
                StepFields = new List<SimulatorStepField> {
                        new SimulatorStepField {
                            StepType = "get/set",
                            Fields = new List<SimulatorStepFieldParam> {
                                new SimulatorStepFieldParam {
                                    Name = "objectName",
                                    Label = "Simulation Object Name",
                                    Info = "Enter the name of the DWSIM object, i.e. Feed",
                                },
                                new SimulatorStepFieldParam {
                                    Name = "objectProperty",
                                    Label = "Simulation Object Property",
                                    Info = "Enter the property of the DWSIM object, i.e. Temperature",
                                },
                            },
                        },
                        new SimulatorStepField {
                            StepType = "command",
                            Fields = new List<SimulatorStepFieldParam> {
                                new SimulatorStepFieldParam {
                                    Name = "command",
                                    Label = "Command",
                                    Info = "Select a command",
                                    Options = new List<SimulatorStepFieldOption> {
                                        new SimulatorStepFieldOption {
                                            Label = "Solve Flowsheet",
                                            Value = "Solve",
                                        }
                                    },
                                },
                            },
                        },
                    },
                UnitQuantities = new List<SimulatorUnitQuantity> {
                    new SimulatorUnitQuantity {
                        Label = "Acceleration",
                        Name = "accel",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m/s²",
                                Name = "m/s2",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm/s²",
                                Name = "cm/s2",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/s²",
                                Name = "ft/s2",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Activity",
                        Name = "activity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Activity Coefficient",
                        Name = "activityCoefficient",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Area",
                        Name = "area",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m²",
                                Name = "m2",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm²",
                                Name = "cm2",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft²",
                                Name = "ft2",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Boiling Point Temperature",
                        Name = "boilingPointTemperature",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Cake Resistance",
                        Name = "cakeresistance",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m/kg",
                                Name = "m/kg",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/lbm",
                                Name = "ft/lbm",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm/g",
                                Name = "cm/g",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Kinematic Viscosity",
                        Name = "cinematic_viscosity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m²/s",
                                Name = "m2/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cSt",
                                Name = "cSt",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft²/s",
                                Name = "ft2/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "mm²/s",
                                Name = "mm2/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm²/s",
                                Name = "cm2/s",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Compressibility",
                        Name = "compressibility",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "1/Pa",
                                Name = "1/Pa",
                            },
                            new SimulatorUnitEntry {
                                Label = "1/atm",
                                Name = "1/atm",
                            },
                            new SimulatorUnitEntry {
                                Label = "1/kPa",
                                Name = "1/kPa",
                            },
                            new SimulatorUnitEntry {
                                Label = "1/bar",
                                Name = "1/bar",
                            },
                            new SimulatorUnitEntry {
                                Label = "1/MPa",
                                Name = "1/MPa",
                            },
                            new SimulatorUnitEntry {
                                Label = "1/psi",
                                Name = "1/psi",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Compressibility Factor",
                        Name = "compressibilityFactor",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Conductance",
                        Name = "conductance",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "[kg/s]/[Pa^0.5]",
                                Name = "[kg/s]/[Pa^0.5]",
                            },
                            new SimulatorUnitEntry {
                                Label = "[lbm/h]/[psi^0.5]",
                                Name = "[lbm/h]/[psi^0.5]",
                            },
                            new SimulatorUnitEntry {
                                Label = "[kg/h]/[atm^0.5]",
                                Name = "[kg/h]/[atm^0.5]",
                            },
                            new SimulatorUnitEntry {
                                Label = "[kg/h]/[bar^0.5]",
                                Name = "[kg/h]/[bar^0.5]",
                            },
                            new SimulatorUnitEntry {
                                Label = "[kg/h]/[[kgf/cm²]^0.5]",
                                Name = "[kg/h]/[[kgf/cm2]^0.5]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Delta P",
                        Name = "deltaP",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "Pa",
                                Name = "Pa",
                            },
                            new SimulatorUnitEntry {
                                Label = "atm",
                                Name = "atm",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbf/ft²",
                                Name = "lbf/ft2",
                            },
                            new SimulatorUnitEntry {
                                Label = "kgf/cm²",
                                Name = "kgf/cm2",
                            },
                            new SimulatorUnitEntry {
                                Label = "kgf/cm²(g)",
                                Name = "kgf/cm2_g",
                            },
                            new SimulatorUnitEntry {
                                Label = "kPa",
                                Name = "kPa",
                            },
                            new SimulatorUnitEntry {
                                Label = "bar",
                                Name = "bar",
                            },
                            new SimulatorUnitEntry {
                                Label = "bar(g)",
                                Name = "barg",
                            },
                            new SimulatorUnitEntry {
                                Label = "ftH₂O",
                                Name = "ftH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "inH₂O",
                                Name = "inH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "inHg",
                                Name = "inHg",
                            },
                            new SimulatorUnitEntry {
                                Label = "mbar",
                                Name = "mbar",
                            },
                            new SimulatorUnitEntry {
                                Label = "mH₂O",
                                Name = "mH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "mmH₂O",
                                Name = "mmH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "mmHg",
                                Name = "mmHg",
                            },
                            new SimulatorUnitEntry {
                                Label = "MPa",
                                Name = "MPa",
                            },
                            new SimulatorUnitEntry {
                                Label = "psi",
                                Name = "psi",
                            },
                            new SimulatorUnitEntry {
                                Label = "psi(g)",
                                Name = "psig",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Delta T",
                        Name = "deltaT",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "°C",
                                Name = "C.",
                            },
                            new SimulatorUnitEntry {
                                Label = "K",
                                Name = "K.",
                            },
                            new SimulatorUnitEntry {
                                Label = "°F",
                                Name = "F.",
                            },
                            new SimulatorUnitEntry {
                                Label = "°R",
                                Name = "R.",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Density",
                        Name = "density",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kg/m³",
                                Name = "kg/m3",
                            },
                            new SimulatorUnitEntry {
                                Label = "g/cm³",
                                Name = "g/cm3",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbm/ft³",
                                Name = "lbm/ft3",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Diameter",
                        Name = "diameter",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "mm",
                                Name = "mm",
                            },
                            new SimulatorUnitEntry {
                                Label = "in",
                                Name = "in",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Diffusivity",
                        Name = "diffusivity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m²/s",
                                Name = "m2/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cSt",
                                Name = "cSt",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft²/s",
                                Name = "ft2/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "mm²/s",
                                Name = "mm2/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm²/s",
                                Name = "cm2/s",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Distance",
                        Name = "distance",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m",
                                Name = "m",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft",
                                Name = "ft",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm",
                                Name = "cm",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Enthalpy",
                        Name = "enthalpy",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kJ/kg",
                                Name = "kJ/kg",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/g",
                                Name = "cal/g",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/lbm",
                                Name = "BTU/lbm",
                            },
                            new SimulatorUnitEntry {
                                Label = "kcal/kg",
                                Name = "kcal/kg",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Entropy",
                        Name = "entropy",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kJ/[kg.K]",
                                Name = "kJ/[kg.K]",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/[g.C]",
                                Name = "cal/[g.C]",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/[lbm.R]",
                                Name = "BTU/[lbm.R]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Excess Enthalpy",
                        Name = "excessEnthalpy",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Excess Entropy",
                        Name = "excessEntropy",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Force",
                        Name = "force",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "N",
                                Name = "N",
                            },
                            new SimulatorUnitEntry {
                                Label = "dyn",
                                Name = "dyn",
                            },
                            new SimulatorUnitEntry {
                                Label = "kgf",
                                Name = "kgf",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbf",
                                Name = "lbf",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Fouling Factor",
                        Name = "foulingfactor",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "K.m²/W",
                                Name = "K.m2/W",
                            },
                            new SimulatorUnitEntry {
                                Label = "C.cm².s/cal",
                                Name = "C.cm2.s/cal",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft².h.F/BTU",
                                Name = "ft2.h.F/BTU",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Fugacity",
                        Name = "fugacity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Fugacity Coefficient",
                        Name = "fugacityCoefficient",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "GOR",
                        Name = "gor",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Head",
                        Name = "head",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m",
                                Name = "m",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft",
                                Name = "ft",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm",
                                Name = "cm",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Cp",
                        Name = "heatCapacityCp",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kJ/[kg.K]",
                                Name = "kJ/[kg.K]",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/[g.C]",
                                Name = "cal/[g.C]",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/[lbm.R]",
                                Name = "BTU/[lbm.R]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Cv",
                        Name = "heatCapacityCv",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Heat Transfer Coefficient",
                        Name = "heat_transf_coeff",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "W/[m².K]",
                                Name = "W/[m2.K]",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/[cm².s.C]",
                                Name = "cal/[cm2.s.C]",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/[ft².h.R]",
                                Name = "BTU/[ft2.h.R]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Heat Flow",
                        Name = "heatflow",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kW",
                                Name = "kW",
                            },
                            new SimulatorUnitEntry {
                                Label = "kcal/h",
                                Name = "kcal/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/h",
                                Name = "BTU/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/s",
                                Name = "BTU/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/s",
                                Name = "cal/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "HP",
                                Name = "HP",
                            },
                            new SimulatorUnitEntry {
                                Label = "kJ/h",
                                Name = "kJ/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "kJ/d",
                                Name = "kJ/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "MW",
                                Name = "MW",
                            },
                            new SimulatorUnitEntry {
                                Label = "W",
                                Name = "W",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/d",
                                Name = "BTU/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "MMBTU/d",
                                Name = "MMBTU/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "MMBTU/h",
                                Name = "MMBTU/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "kcal/s",
                                Name = "kcal/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "kcal/h",
                                Name = "kcal/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "kcal/d",
                                Name = "kcal/d",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Ideal Gas Heat Capacity",
                        Name = "idealGasHeatCapacity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Joule Thomson Coefficient",
                        Name = "jouleThomsonCoefficient",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "K/Pa",
                                Name = "K/Pa",
                            },
                            new SimulatorUnitEntry {
                                Label = "°F/psi",
                                Name = "F/psi",
                            },
                            new SimulatorUnitEntry {
                                Label = "°C/atm",
                                Name = "C/atm",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "K Value",
                        Name = "kvalue",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "log Fugacity Coefficient",
                        Name = "logFugacityCoefficient",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "log K Value",
                        Name = "logKvalue",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Mass",
                        Name = "mass",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kg",
                                Name = "kg",
                            },
                            new SimulatorUnitEntry {
                                Label = "g",
                                Name = "g",
                            },
                            new SimulatorUnitEntry {
                                Label = "lb",
                                Name = "lb",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Mass Concentration",
                        Name = "mass_conc",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kg/m³",
                                Name = "kg/m3",
                            },
                            new SimulatorUnitEntry {
                                Label = "g/L",
                                Name = "g/L",
                            },
                            new SimulatorUnitEntry {
                                Label = "g/cm³",
                                Name = "g/cm3",
                            },
                            new SimulatorUnitEntry {
                                Label = "g/mL",
                                Name = "g/mL",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbm/ft³",
                                Name = "lbm/ft3",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Mass Flow",
                        Name = "massflow",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "g/s",
                                Name = "g/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbm/h",
                                Name = "lbm/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "kg/s",
                                Name = "kg/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "kg/h",
                                Name = "kg/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "kg/d",
                                Name = "kg/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "kg/min",
                                Name = "kg/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "lb/min",
                                Name = "lb/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "lb/s",
                                Name = "lb/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "lb/h",
                                Name = "lb/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "lb/d",
                                Name = "lb/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "Mg/s",
                                Name = "Mg/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "Mg/h",
                                Name = "Mg/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "Mg/d",
                                Name = "Mg/d",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Mass Fraction",
                        Name = "massfraction",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Medium Resistance",
                        Name = "mediumresistance",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m-1",
                                Name = "m-1",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm-1",
                                Name = "cm-1",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft-1",
                                Name = "ft-1",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Melting Temperature",
                        Name = "meltingTemperature",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Molar Concentration",
                        Name = "molar_conc",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kmol/m³",
                                Name = "kmol/m3",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/m³",
                                Name = "mol/m3",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/L",
                                Name = "mol/L",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/cm³",
                                Name = "mol/cm3",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/mL",
                                Name = "mol/mL",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbmol/ft³",
                                Name = "lbmol/ft3",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Molar Enthalpy",
                        Name = "molar_enthalpy",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kJ/kmol",
                                Name = "kJ/kmol",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/mol",
                                Name = "cal/mol",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/lbmol",
                                Name = "BTU/lbmol",
                            },
                            new SimulatorUnitEntry {
                                Label = "J/mol",
                                Name = "J/mol",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Molar Entropy",
                        Name = "molar_entropy",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kJ/[kmol.K]",
                                Name = "kJ/[kmol.K]",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/[mol.C]",
                                Name = "cal/[mol.C]",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/[lbmol.R]",
                                Name = "BTU/[lbmol.R]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Molar Volume",
                        Name = "molar_volume",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m³/kmol",
                                Name = "m3/kmol",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm³/mmol",
                                Name = "cm3/mmol",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³/lbmol",
                                Name = "ft3/lbmol",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Molar Flow",
                        Name = "molarflow",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "mol/s",
                                Name = "mol/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbmol/h",
                                Name = "lbmol/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/h",
                                Name = "mol/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/d",
                                Name = "mol/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "kmol/s",
                                Name = "kmol/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "kmol/h",
                                Name = "kmol/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "kmol/d",
                                Name = "kmol/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d @ BR",
                                Name = "m3/d @ BR",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d @ NC",
                                Name = "m3/d @ NC",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d @ CNTP",
                                Name = "m3/d @ CNTP",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d @ SC",
                                Name = "m3/d @ SC",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d @ 0 °C, 1 atm",
                                Name = "m3/d @ 0 C, 1 atm",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d @ 15.56 °C, 1 atm",
                                Name = "m3/d @ 15.56 C, 1 atm",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d @ 20 °C, 1 atm",
                                Name = "m3/d @ 20 C, 1 atm",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³/d @ 60 °F, 14.7 psia",
                                Name = "ft3/d @ 60 f, 14.7 psia",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³/d @ 0 °C, 1 atm",
                                Name = "ft3/d @ 0 C, 1 atm",
                            },
                            new SimulatorUnitEntry {
                                Label = "MMSCFD",
                                Name = "MMSCFD",
                            },
                            new SimulatorUnitEntry {
                                Label = "SCFD",
                                Name = "SCFD",
                            },
                            new SimulatorUnitEntry {
                                Label = "SCFM",
                                Name = "SCFM",
                            },
                            new SimulatorUnitEntry {
                                Label = "Mm³/d @ BR",
                                Name = "Mm3/d @ BR",
                            },
                            new SimulatorUnitEntry {
                                Label = "Mm³/d @ SC",
                                Name = "Mm3/d @ SC",
                            },
                            new SimulatorUnitEntry {
                                Label = "Mm³/d @ NC",
                                Name = "Mm3/d @ NC",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Molar Fraction",
                        Name = "molarfraction",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Molecular Weight",
                        Name = "molecularWeight",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kg/kmol",
                                Name = "kg/kmol",
                            },
                            new SimulatorUnitEntry {
                                Label = "g/mol",
                                Name = "g/mol",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbm/lbmol",
                                Name = "lbm/lbmol",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "None",
                        Name = "none",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Pressure",
                        Name = "pressure",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "Pa",
                                Name = "Pa",
                            },
                            new SimulatorUnitEntry {
                                Label = "atm",
                                Name = "atm",
                            },
                            new SimulatorUnitEntry {
                                Label = "kgf/cm²",
                                Name = "kgf/cm2",
                            },
                            new SimulatorUnitEntry {
                                Label = "kgf/cm²(g)",
                                Name = "kgf/cm2g",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbf/ft²",
                                Name = "lbf/ft2",
                            },
                            new SimulatorUnitEntry {
                                Label = "kPa",
                                Name = "kPa",
                            },
                            new SimulatorUnitEntry {
                                Label = "kPa(g)",
                                Name = "kPag",
                            },
                            new SimulatorUnitEntry {
                                Label = "bar",
                                Name = "bar",
                            },
                            new SimulatorUnitEntry {
                                Label = "bar(g)",
                                Name = "barg",
                            },
                            new SimulatorUnitEntry {
                                Label = "ftH₂O",
                                Name = "ftH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "inH₂O",
                                Name = "inH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "inHg",
                                Name = "inHg",
                            },
                            new SimulatorUnitEntry {
                                Label = "mbar",
                                Name = "mbar",
                            },
                            new SimulatorUnitEntry {
                                Label = "mH₂O",
                                Name = "mH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "mmH₂O",
                                Name = "mmH2O",
                            },
                            new SimulatorUnitEntry {
                                Label = "mmHg",
                                Name = "mmHg",
                            },
                            new SimulatorUnitEntry {
                                Label = "MPa",
                                Name = "MPa",
                            },
                            new SimulatorUnitEntry {
                                Label = "psi",
                                Name = "psi",
                            },
                            new SimulatorUnitEntry {
                                Label = "psi(g)",
                                Name = "psig",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Reaction Rate",
                        Name = "reac_rate",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kmol/[m³.s]",
                                Name = "kmol/[m3.s]",
                            },
                            new SimulatorUnitEntry {
                                Label = "kmol/[m³.min]",
                                Name = "kmol/[m3.min.]",
                            },
                            new SimulatorUnitEntry {
                                Label = "kmol/[m³.h]",
                                Name = "kmol/[m3.h]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[m³.s]",
                                Name = "mol/[m3.s]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[m³.min]",
                                Name = "mol/[m3.min.]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[m³.h]",
                                Name = "mol/[m3.h]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[L.s]",
                                Name = "mol/[L.s]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[L.min]",
                                Name = "mol/[L.min.]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[L.h]",
                                Name = "mol/[L.h]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[cm³.s]",
                                Name = "mol/[cm3.s]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[cm³.min]",
                                Name = "mol/[cm3.min.]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[cm³.h]",
                                Name = "mol/[cm3.h]",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbmol/[ft³.h]",
                                Name = "lbmol/[ft3.h]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Reaction Rate Heterogeneous",
                        Name = "reac_rate_heterog",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kmol/[kg.s]",
                                Name = "kmol/[kg.s]",
                            },
                            new SimulatorUnitEntry {
                                Label = "kmol/[kg.min.]",
                                Name = "kmol/[kg.min.]",
                            },
                            new SimulatorUnitEntry {
                                Label = "kmol/[kg.h]",
                                Name = "kmol/[kg.h]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[kg.s]",
                                Name = "mol/[kg.s]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[kg.min.]",
                                Name = "mol/[kg.min.]",
                            },
                            new SimulatorUnitEntry {
                                Label = "mol/[kg.h]",
                                Name = "mol/[kg.h]",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbmol/[lbm.h]",
                                Name = "lbmol/[lbm.h]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Specific Volume",
                        Name = "spec_vol",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m³/kg",
                                Name = "m3/kg",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm³/g",
                                Name = "cm3/g",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³/lbm",
                                Name = "ft3/lbm",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Speed of Sound",
                        Name = "speedOfSound",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m/s",
                                Name = "m/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm/s",
                                Name = "cm/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "mm/s",
                                Name = "mm/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "km/h",
                                Name = "km/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/h",
                                Name = "ft/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/min",
                                Name = "ft/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/s",
                                Name = "ft/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "in/s",
                                Name = "in/s",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Surface Tension",
                        Name = "surfaceTension",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "N/m",
                                Name = "N/m",
                            },
                            new SimulatorUnitEntry {
                                Label = "dyn/cm",
                                Name = "dyn/cm",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbf/in",
                                Name = "lbf/in",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Temperature",
                        Name = "temperature",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "K",
                                Name = "K",
                            },
                            new SimulatorUnitEntry {
                                Label = "°R",
                                Name = "R",
                            },
                            new SimulatorUnitEntry {
                                Label = "°C",
                                Name = "C",
                            },
                            new SimulatorUnitEntry {
                                Label = "°F",
                                Name = "F",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Thermal Conductivity",
                        Name = "thermalConductivity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "W/[m.K]",
                                Name = "W/[m.K]",
                            },
                            new SimulatorUnitEntry {
                                Label = "cal/[cm.s.C]",
                                Name = "cal/[cm.s.C]",
                            },
                            new SimulatorUnitEntry {
                                Label = "BTU/[ft.h.R]",
                                Name = "BTU/[ft.h.R]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Liquid Thermal Conductivity",
                        Name = "thermalConductivityOfLiquid",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Vapor Thermal Conductivity",
                        Name = "thermalConductivityOfVapor",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Thickness",
                        Name = "thickness",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Time",
                        Name = "time",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "s",
                                Name = "s",
                            },
                            new SimulatorUnitEntry {
                                Label = "min",
                                Name = "min.",
                            },
                            new SimulatorUnitEntry {
                                Label = "h",
                                Name = "h",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Vapor Pressure",
                        Name = "vaporPressure",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Velocity",
                        Name = "velocity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m/s",
                                Name = "m/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm/s",
                                Name = "cm/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "mm/s",
                                Name = "mm/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "km/h",
                                Name = "km/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/h",
                                Name = "ft/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/min",
                                Name = "ft/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft/s",
                                Name = "ft/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "in/s",
                                Name = "in/s",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Viscosity",
                        Name = "viscosity",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "kg/[m.s]",
                                Name = "kg/[m.s]",
                            },
                            new SimulatorUnitEntry {
                                Label = "Pa.s",
                                Name = "Pa.s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cP",
                                Name = "cP",
                            },
                            new SimulatorUnitEntry {
                                Label = "lbm/[ft.h]",
                                Name = "lbm/[ft.h]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Liquid Viscosity",
                        Name = "viscosityOfLiquid",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Vapor Viscosity",
                        Name = "viscosityOfVapor",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "",
                                Name = "",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Volume",
                        Name = "volume",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m³",
                                Name = "m3",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm³",
                                Name = "cm3",
                            },
                            new SimulatorUnitEntry {
                                Label = "L",
                                Name = "L",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³",
                                Name = "ft3",
                            },
                            new SimulatorUnitEntry {
                                Label = "bbl",
                                Name = "bbl",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[US]",
                                Name = "gal[US]",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[UK]",
                                Name = "gal[UK]",
                            },
                        }
                    },
                    new SimulatorUnitQuantity {
                        Label = "Volumetric Flow",
                        Name = "volumetricFlow",
                        Units = new List<SimulatorUnitEntry> {
                            new SimulatorUnitEntry {
                                Label = "m³/s",
                                Name = "m3/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³/s",
                                Name = "ft3/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "cm³/s",
                                Name = "cm3/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/h",
                                Name = "m3/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "m³/d",
                                Name = "m3/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "bbl/h",
                                Name = "bbl/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "bbl/d",
                                Name = "bbl/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³/min",
                                Name = "ft3/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "ft³/d",
                                Name = "ft3/d",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[UK]/h",
                                Name = "gal[UK]/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[UK]/min",
                                Name = "gal[UK]/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[UK]/s",
                                Name = "gal[UK]/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[US]/h",
                                Name = "gal[US]/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[US]/min",
                                Name = "gal[US]/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "gal[US]/s",
                                Name = "gal[US]/s",
                            },
                            new SimulatorUnitEntry {
                                Label = "L/h",
                                Name = "L/h",
                            },
                            new SimulatorUnitEntry {
                                Label = "L/min",
                                Name = "L/min",
                            },
                            new SimulatorUnitEntry {
                                Label = "L/s",
                                Name = "L/s",
                            },
                        }
                    },
                }


            };
        }
    }
}