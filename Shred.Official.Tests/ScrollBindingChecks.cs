using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using Realms;

internal static class ScrollBindingChecks
{
    public static void Run()
    {
        var defaults=new ShredRuleset().GetDefaultKeyBindings().ToArray();
        int slower=Array.FindIndex(defaults,b=>(ShredAction)b.Action==ShredAction.ScrollSlower);
        int faster=Array.FindIndex(defaults,b=>(ShredAction)b.Action==ShredAction.ScrollFaster);
        if(slower>=faster || !defaults[slower].KeyCombination.Equals(new KeyCombination(InputKey.F3)) || !defaults[faster].KeyCombination.Equals(new KeyCombination(InputKey.F4)))throw new Exception("Scroll defaults/order");
        var config=new RealmConfiguration(Path.Combine(Path.GetTempPath(),"shred-scroll-"+Guid.NewGuid()+".realm"));
        try
        {
            using(var realm=Realm.GetInstance(config))
            {
                var up=new RealmKeyBinding((int)ShredAction.ScrollFaster,new KeyCombination(InputKey.F3),"shred",0);
                var down=new RealmKeyBinding((int)ShredAction.ScrollSlower,new KeyCombination(InputKey.F4),"shred",0);
                realm.Write(()=>{realm.Add(up);realm.Add(down);ShredScrollBindingMigration.Apply(realm);});
                if(!up.KeyCombination.Equals(new KeyCombination(InputKey.F4)) || !down.KeyCombination.Equals(new KeyCombination(InputKey.F3)))throw new Exception("Persisted bindings not migrated");
                realm.Write(()=>ShredScrollBindingMigration.Apply(realm));
                if(!down.KeyCombination.Equals(new KeyCombination(InputKey.F3)))throw new Exception("Migration not idempotent");
                realm.Write(()=>{up.KeyCombination=new KeyCombination(InputKey.F6);ShredScrollBindingMigration.Apply(realm);});
                if(!up.KeyCombination.Equals(new KeyCombination(InputKey.F6)))throw new Exception("Custom binding overwritten");
            }
        }
        finally{Realm.DeleteRealm(config);}
        Console.WriteLine("PASS F3 decrease / F4 increase defaults, display order, real Realm migration, idempotence and custom bindings");
    }
}
