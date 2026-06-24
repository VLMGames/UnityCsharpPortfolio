# Unity Factory Automation Prototype

This project is a prototype of a factory automation system built in Unity. The goal is to demonstrate proficiency in game system architecture, custom editor tools, and component-based interactions.

## 🛠 Key Features

### 🏗 Placement System
* **Flexible Building:** Supports free-form object placement with real-time validation.
* **Validation Logic:** Uses `PlacementTool` and `FloorOrWallDetector` to ensure buildings are placed in valid locations, preventing overlaps or geometry violations.
* **Management:** `BuildingManager` and `BuildingUI` handle the spawning and interaction logic for all placed units.

### ⚙️ Conveyor System
* **Spline-based Movement:** Uses `SplineContainer` to build custom conveyor paths.
* **Custom Editor Tool:** `ConveyorBeltTool` allows for intuitive placement and snapping of conveyor nodes directly in the Unity Editor.
* **Dynamic Logic:** `ConveyorBelt` handles runtime movement, ensuring seamless item transport across the factory line.

### 🏭 Production Logic
* **Modular Architecture:** `ProductionBuilding` implements `IConveyorNode`, making the system easily extensible for different production types.
* **Data-driven Design:** Uses `ItemRequirement` and recipe lists for flexible economic balancing.
* **Seamless Ports:** Logic for input/output ports enables direct item transfer between buildings and conveyor belts.

## 💻 Technical Stack
* **Engine:** Unity
* **Language:** C#
* **Design Patterns:** Component-based architecture, Interface-driven development, Observer pattern.
* **Version Control:** Git
