public abstract class PlayerModule
{
    protected PlayerManager manager;
    public PlayerModule(PlayerManager manager) { this.manager = manager; }
    public virtual void OnAwake() { }
    public virtual void OnStart() { }
    public virtual void OnUpdate() { }
}
