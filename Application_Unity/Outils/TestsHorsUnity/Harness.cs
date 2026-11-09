// =====================================================================================
//  Harness.cs — LANCE LES TESTS EDITMODE « PURS » SANS L'ÉDITEUR UNITY
//
//  RÔLE
//    Charge l'assembly de tests compilée (voir lancer.sh), exécute par réflexion les
//    méthodes [Test] des classes dont le nom finit par l'argument 2, affiche OK / FAIL.
//    Ne marche que pour les calculs purs : un test qui crée un GameObject ou appelle du
//    code natif (Quaternion.Euler, AngleAxis…) échoue ici, c'est normal (le lancer dans Unity).
// =====================================================================================
using System; using System.IO; using System.Linq; using System.Reflection;
public static class Harness {
    static string[] dirs;
    public static int Main(string[] a) {
        dirs = a.Skip(2).ToArray();
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            string n = new AssemblyName(e.Name).Name + ".dll";
            foreach (var d in dirs) { var p = Path.Combine(d, n); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null; };
        var asm = Assembly.LoadFrom(a[0]); int ok = 0, ko = 0;
        foreach (var t in asm.GetTypes().Where(t => t.FullName.EndsWith(a[1]))) {
            foreach (var m in t.GetMethods().Where(m => m.GetCustomAttributes(true).Any(x => x.GetType().Name == "TestAttribute"))) {
                try { m.Invoke(Activator.CreateInstance(t), null); ok++; Console.WriteLine("OK   " + m.Name); }
                catch (TargetInvocationException ex) { ko++; Console.WriteLine("FAIL " + m.Name + " : " + ex.InnerException.Message.Split('\n')[0]); }
            }
        }
        Console.WriteLine(ok + " reussis, " + ko + " echoues"); return ko;
    }
}
