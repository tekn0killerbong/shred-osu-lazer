using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Game.Rulesets.Shred;
using osuTK.Input;

internal static class KeyboardChecks
{
    public static void Run()
    {
        var bindings=new ShredRuleset().GetDefaultKeyBindings().Where(b=>(int)(ShredAction)b.Action<118).ToArray();
        int cases=0;
        foreach(var first in bindings)
        foreach(var second in bindings.Where(b=>b.Action.ToString()!=first.Action.ToString()))
        {
            using var sink=new Sink();
            using var input=new Harness(bindings,sink);
            var state=new InputState();
            Key a=Enum.GetValues<Key>().First(k=>KeyCombination.FromKey(k)==first.KeyCombination.Keys[0]);
            Key b=Enum.GetValues<Key>().First(k=>KeyCombination.FromKey(k)==second.KeyCombination.Keys[0]);
            state.Keyboard.Keys.SetPressed(a,true);input.Send(new KeyDownEvent(state,a,false));
            state.Keyboard.Keys.SetPressed(b,true);input.Send(new KeyDownEvent(state,b,false));
            if(sink.Active.Count!=2 || input.PressedActions.Count()!=2) throw new Exception($"Keyboard pair lost {a}/{b}");
            state.Keyboard.Keys.SetPressed(b,false);input.Send(new KeyUpEvent(state,b));
            if(!sink.Active.SetEquals(new[]{(ShredAction)first.Action}))throw new Exception($"Remaining key lost {a}/{b}");
            state.Keyboard.Keys.SetPressed(a,false);input.Send(new KeyUpEvent(state,a));
            if(sink.Active.Count!=0)throw new Exception("Stuck key");
            cases++;
        }
        Console.WriteLine($"PASS Actual Shred keyboard container: {cases} ordered pairs retain independent press/release");
    }
    private partial class Harness:ShredInputManager.ShredKeyBindingContainer
    {
        private readonly Drawable sink;
        public Harness(IEnumerable<IKeyBinding> bindings,Drawable sink):base(new ShredRuleset().RulesetInfo,0,SimultaneousBindingMode.Unique){KeyBindings=bindings;this.sink=sink;}
        protected override IEnumerable<Drawable> KeyBindingInputQueue=>new[]{sink};
        public bool Send(UIEvent e)=>Handle(e);
    }
    private partial class Sink:Drawable,IKeyBindingHandler<ShredAction>
    {
        public readonly HashSet<ShredAction> Active=new();
        public bool OnPressed(KeyBindingPressEvent<ShredAction> e){Active.Add(e.Action);return true;}
        public void OnReleased(KeyBindingReleaseEvent<ShredAction> e)=>Active.Remove(e.Action);
    }
}
