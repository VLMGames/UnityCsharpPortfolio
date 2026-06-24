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

## 🎥 Project Demonstrations

Here you can see the systems in action:

* [View Conveyor Placement Demo](https://drive.google.com/file/d/1pv8u85wfScjpQaA6W-kSMM7IQPf2D5rG/view?usp=drive_link)
* [View Building Placement & Logic](https://drive.google.com/file/d/1w_QMEXaj4Y0B4tB4JR8SN5KvnZXGDKzr/view?usp=drive_link)
* [View Production & Automation Flow](https://drive.google.com/file/d/1Qw1bSd9lh6h60EgA5w4ZSp-u1yV49YVb/view?usp=drive_link)

## 💻 Technical Stack
* **Engine:** Unity
* **Language:** C#
* **Design Patterns:** Component-based architecture, Interface-driven development, Observer pattern.
* **Version Control:** Git
