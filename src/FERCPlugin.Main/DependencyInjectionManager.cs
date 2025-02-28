using Ninject.Modules;
using FERCPlugin.Core.Models;

namespace FERCPlugin.Main;

public class DependencyInjectionManager : NinjectModule
{
    public override void Load()
    {
        Bind<IApplicationDataProperties>().To<ApplicationDataProperties>().InSingletonScope();
    }
}
