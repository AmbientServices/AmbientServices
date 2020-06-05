# AmbientServices
The AmbientServices library is a library that allows library code to access basic services which are both universal and optional, allowing those libraries to be used in a variety of systems that provide vastly different implementations (or no implementation) of those basic services.

These basic services include logging, settings, caching, and progress tracking.  
By accessing these services through the interfaces provided by AmbientServices, code integrators can use the libraries without having to provide dependencies.
If integrators want the added benefits provided by a centralized implementation of one or more of those services, they can provide a bridge to their own implementations of these services and register them with the ambient service registry.
With one simple registration, the services will automatically be utilized by every library that uses AmbientServices to access them.

The well known dependency injection pattern is one possible solution to this problem, but dependency injection requires the code consumer to pass the required dependencies to each object constructor, which can be cumbersome, especially when the functionality is optional anyway.
Dependency injection becomes even more cumbersome when the code adds or removes dependencies, requiring the code user to update every constructor invocation with the new dependencies.

While dependency injection is better in most circumstances, when the services are completely optional and so pervasive as to be required by nearly all significant functionality, ambient services is a better solution.

## Authors and license
This library is licensed under [MIT](https://opensource.org/licenses/MIT).

The library was written by James Ivie.

## Language and Tools
The code is written in C#, using .NET Standard.

The code is built using either Microsoft Visual Studio 2017+ or Microsoft Visual Sutdio Code.