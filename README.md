# eduSignalFormatter
Simplistic server to test the transmission of the serialized sensor data via **BioSemi Data Format (BDF)** from the **eduSignalFW** firmware project.
Communicates with platform-board for a defined period of time and writes the received sensor data directly to the file system. File can then be 
opened with e.g. EDFBrowser.
## Usage
Uses .Net Core 3.1. Definitely should work with any Visual Studio Version 17 e.g. Visual Studio 2022 with .Net Core installed.
