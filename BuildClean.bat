rd .vs /s /q
for %%p in (AmbientServices AmbientServices.Samples AmbientServices.Test AmbientServices.Test.DelayedLoad ReflectionTypeLoadException.Assembly) do for %%f in (obj bin\Debug bin\Release) do rd %%p\%%f /s /q
for %%p in (AmbientServices AmbientServices.Samples AmbientServices.Test AmbientServices.Test.DelayedLoad ReflectionTypeLoadException.Assembly) do del %%p\packages.config /q
rd packages /s /q
rd TestResults /s /q
for %%p in (AmbientServices AmbientServices.Samples AmbientServices.Test AmbientServices.Test.DelayedLoad ReflectionTypeLoadException.Assembly) do rd %%p\TestResults /s /q

