/// <summary>
/// Manager 基类：GameFace 下的各功能管理器都继承此类。
/// </summary>
public class BaseManager
{
    protected GameFace face;

    public BaseManager(GameFace face)
    {
        this.face = face;
    }

    public virtual void OnInit() { }
    public virtual void OnDistory() { }
}
