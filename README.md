# Strategical Robot Distribution Simulator (SRDS)
![Static Badge](https://img.shields.io/badge/MSPripotnev-SRDS-StrategicalRobotDistrubutionSimulator)
![GitHub top language](https://img.shields.io/github/languages/top/MSPripotnev/StrategicalRobotDistrubutionSimulator)
![GitHub code size in bytes](https://img.shields.io/github/languages/code-size/MSPripotnev/StrategicalRobotDistrubutionSimulator)
![GitHub repo size](https://img.shields.io/github/repo-size/MSPripotnev/StrategicalRobotDistrubutionSimulator)
![GitHub last commit](https://img.shields.io/github/last-commit/MSPripotnev/StrategicalRobotDistrubutionSimulator)

## Purpose
The program is designed to build scenarios for the operation of a multi-agent system of intelligent mobile robots that allow you to explore the effectiveness and features of various algorithms for building routes, distributing tasks, and scheduling.

## Application field
The field of application is the educational and scientific process of higher education in specialities and areas of training related to algorithms for building routes and controlling a group of robots.

## Functionality
The software package provides the following functions:

* Building an environment model in the form of a terrain map with mapped key points, roads and obstacles
* Simulating weather conditions:
  * temperature, humidity, pressure, wind and their influence to map targets
  * clouds approximated by gaussians with intensity increasing in their live zone and time
* Building routes for individual robots by A* algorithm
* Distributing tasks between robots using various algorithms, according to the highest efficiency
* Using fuzzy logic neural network algorithms to calculate agent-target efficiency
* Schedule plans as a set of agents time-distributed actions
* Calculate and record efficiency of each solution on several attempts and different testing models