namespace TrashMan
{
    public interface ITrashTitanRoleController
    {
        TrashTitanRole Role { get; }
        void TickRoleInput(float deltaTime);
    }
}
