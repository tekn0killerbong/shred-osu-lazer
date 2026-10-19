using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Scoring;
using Realms;

internal static class NativeIdentityChecks
{
    public static void Run(Beatmap decoded)
    {
        string folder=Path.Combine(Path.GetTempPath(),"shred-identity-"+Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var config=new RealmConfiguration(Path.Combine(folder,"test.realm"));
        Guid id=Guid.NewGuid(); Guid scoreID;
        var original=decoded.BeatmapInfo;
        try
        {
            using(var realm=Realm.GetInstance(config))
            {
                var standard=new osu.Game.Rulesets.Osu.OsuRuleset().RulesetInfo;
                var shred=new ShredRuleset().RulesetInfo;
                var map=new BeatmapInfo { ID=id,Hash="unchanged-source-hash",MD5Hash="unchanged-md5",Ruleset=standard,StarRating=2.5 };
                var other=new BeatmapInfo { Ruleset=standard,Hash="other",StarRating=7 };
                var score=new ScoreInfo(map,shred) { TotalScore=123456 };
                scoreID=score.ID;
                realm.Write(()=>{realm.Add(standard);realm.Add(shred);realm.Add(map);realm.Add(other);realm.Add(score);});
                var selection=ShredNativeIdentity.Validate(map,decoded);
                realm.Write(()=>ShredNativeIdentity.ApplyInTransaction(realm,selection,true));
                Assert(map.Ruleset.ShortName=="shred" && map.Ruleset.OnlineID==-1 && map.StarRating==-1,"Actual Realm transaction assigns custom identity");
                Assert(map.Hash=="unchanged-source-hash" && map.MD5Hash=="unchanged-md5" && other.Ruleset.ShortName=="osu" && other.StarRating==7 && score.TotalScore==123456 && score.BeatmapInfo.ID==id,"Files, other difficulties and existing score preserved");
                try {ShredNativeIdentity.Validate(other,new Beatmap());throw new Exception("Unmarked accepted");}catch(InvalidOperationException){}
                try {realm.Write(()=>ShredNativeIdentity.ApplyInTransaction(realm,selection,false));throw new Exception("Stale snapshot accepted");}catch(InvalidOperationException){}
                Assert(map.Ruleset.ShortName=="shred","Stale selection rejected without mutation");
            }
            using(var realm=Realm.GetInstance(config))
            {
                var map=realm.Find<BeatmapInfo>(id)!;
                Assert(map.Ruleset.ShortName=="shred" && realm.Find<ScoreInfo>(scoreID)!.TotalScore==123456,"Identity and score survive database close/reopen");
                Assert(map.AllowGameplayWithRuleset(new ShredRuleset().RulesetInfo,false),"Native identity is playable without Show converted beatmaps");
                // Real decoder output is still a standard carrier; local identity must take precedence.
                decoded.BeatmapInfo=map.Detach();
                var working=new FlatWorkingBeatmap(decoded);
                var playable=working.GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
                Assert(playable.HitObjects.Count==decoded.HitObjects.Count,"Native catalogue identity remains playable through WorkingBeatmap");
                var selection=ShredNativeIdentity.Validate(map,decoded);
                realm.Write(()=>ShredNativeIdentity.ApplyInTransaction(realm,selection,false));
                Assert(map.Ruleset.ShortName=="osu" && map.Hash=="unchanged-source-hash","Reverse transaction restores standard without rewriting source");
                realm.Write(()=>ShredAutoImport.SetManualChoice(realm,map,false));
                Assert(ShredAutoImport.IsSuppressed(realm,map),"Automatic classification respects manual standard choice");
                realm.Write(()=>ShredAutoImport.SetManualChoice(realm,map,true));
                Assert(!ShredAutoImport.IsSuppressed(realm,map),"Manual native choice clears automatic classification override");
                var empty=new Beatmap();
                Assert(!ShredAutoImport.IsAuthored(empty),"Ordinary empty standard map is not classified Shred");
                empty.Metadata.Tags=ShredAutoImport.Tag;
                Assert(ShredAutoImport.IsAuthored(empty),"Explicitly tagged empty Shred export is recognised");
            }
        }
        finally {decoded.BeatmapInfo=original;Realm.DeleteRealm(config);Directory.Delete(folder,true);}
    }
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
