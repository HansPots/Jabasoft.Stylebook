namespace Stylebook.Data.Entities;

/// <summary>How a component's Testcontainer preview treats its own width/height - see StylebookComponent.TestContainerWidthMode/TestContainerHeightMode.</summary>
public enum ContainerSizeMode
{
    /// <summary>"Vast (eigen breedte/hoogte)" - the component keeps its own (XAML-defined) size, centered in the container.</summary>
    Fixed,

    /// <summary>"Variabel (vult container)" - the component stretches to fill the container's size.</summary>
    Variable,
}
