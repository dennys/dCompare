using CommunityToolkit.Mvvm.ComponentModel;

namespace dCompare.Infrastructure
{
    public abstract class ViewModelBase : ObservableObject
    {
        public virtual bool IsValid => true;
    }
}
