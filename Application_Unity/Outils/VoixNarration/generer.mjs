// =====================================================================================
//  generer.mjs — VOIX DE LA NARRATION (ElevenLabs)
//
//  RÔLE
//    Lit toutes les répliques dans les scènes Unity (textes, consignes, félicitations,
//    présentation), et fait lire par ElevenLabs celles qui n'ont pas encore de voix.
//    Chaque fichier est nommé d'après le texte (MD5) : c'est ainsi que NarrationBulle le
//    retrouve au moment de l'afficher. Changer une phrase demande donc de relancer ce
//    script ; en attendant, la phrase s'affiche simplement sans voix.
//
//  UTILISATION (depuis ce dossier, après « npm install »)
//      npm run liste              répliques trouvées, manquantes, caractères à consommer
//      npm run voix               voix du compte (dont les voix clonées)
//      npm run bibliotheque       voix masculines françaises de la bibliothèque publique
//      npm run essai -- --voix=ID1,ID2     même phrase lue par chaque voix, pour comparer
//      npm run bibliotheque -- --genre=female   voix féminines
//      npm run essai -- --voix=ID1,ID2 --source=tutoriel   essai sur une phrase du tutoriel
//      npm run generer            génère ce qui manque, chaque texte avec la voix de sa source
//                                 (narration des scènes : Kevan ; tutoriel du menu : VOIX_TUTORIEL)
//      npm run generer -- --voix=ID        avec une autre voix
//      npm run generer -- --refaire        regénère tout (ex. changement de voix ou d'interprétation)
//
//  LE JEU D'ACTEUR : interpretation.json associe à un texte AFFICHÉ sa version DITE, avec des
//    balises ([whispers], [pause]…) et de la ponctuation. Le fichier reste nommé d'après le
//    texte affiché : changer une interprétation demande « --refaire », pas de toucher aux scènes.
//    Une réplique absente de ce fichier est lue telle quelle.
//
//  LA CLÉ : ELEVENLABS_API_KEY, lue dans l'environnement (ou le fichier .env du dossier
//    utilisateur, voir package.json). Ne jamais l'écrire dans le code.
//
//  Le jeu n'a besoin ni d'Internet ni de la clé : il lit les MP3 générés ici.
// =====================================================================================
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";

const ICI = dirname(fileURLToPath(import.meta.url));
const PROJET = join(ICI, "..", "..");
const SCENES = ["Menu", "Scene.de.base1", "Scene2", "Scene3"].map((s) => join(PROJET, "Assets/Projet/Scenes_Niveaux", s + ".unity"));
const SORTIE = join(PROJET, "Assets/Projet/Audio/Voice/Resources/Narration");

// Voix de la narration du jeu : Kevan, storytelling grave et sombre (choisie en essai parmi 6 voix).
const VOIX_NARRATION = "AYfOkr1hDQYbhigmRZ16";
// Voix du tutoriel des gestes (menu). null = pas encore choisie : ses textes ne sont pas générés.
const VOIX_TUTORIEL = "dYjOkSQBPiH2igolJfeH"; // Koraly, voix féminine parisienne, claire (choisie en essai)

const SCRIPTS_TUTORIEL = ["TutorielGestes.cs", "TutorielMenu.cs"].map((f) => join(PROJET, "Assets/Projet/Scripts/Jeu", f));
const MODELE = "eleven_v4";
const LANGUE = "fr";

const INTERPRETATION = JSON.parse(readFileSync(join(ICI, "interpretation.json"), "utf8"));

const CHAMPS = /^(\s*)(- )?(texte|consigne|consigneReplacement|felicitation|presentation): ?(.*)$/;

/** Décode une chaîne YAML entre guillemets doubles (Unity y écrit \xE9 pour é). */
function decoderGuillemets(s) {
  return s.replace(/\\(x[0-9a-fA-F]{2}|u[0-9a-fA-F]{4}|.)/g, (_, e) => {
    if (e[0] === "x" || e[0] === "u") return String.fromCharCode(parseInt(e.slice(1), 16));
    return { n: "\n", t: "\t", '"': '"', "\\": "\\", "/": "/" }[e] ?? e;
  });
}

/** Répliques d'une scène. Unity replie les longues lignes : le repli équivaut à une espace. */
export function lireRepliques(contenu) {
  const lignes = contenu.split(/\r?\n/);
  const textes = [];
  for (let i = 0; i < lignes.length; i++) {
    const m = lignes[i].match(CHAMPS);
    if (!m) continue;
    const retrait = m[1].length + (m[2] ? 2 : 0);
    let brut = m[4];
    let texte;

    if (brut.startsWith('"')) {
      // Fermé par un guillemet non échappé, éventuellement quelques lignes plus loin.
      const ferme = (t) => /(^|[^\\])(\\\\)*"$/.test(t) && t.length > 1;
      while (!ferme(brut) && i + 1 < lignes.length) brut += " " + lignes[++i].trim();
      texte = decoderGuillemets(brut.slice(1, -1));
    } else if (brut.startsWith("'")) {
      const ferme = (t) => t.length > 1 && t.endsWith("'") && (t.match(/'+$/)[0].length % 2 === 1);
      while (!ferme(brut) && i + 1 < lignes.length) brut += " " + lignes[++i].trim();
      texte = brut.slice(1, -1).replace(/''/g, "'");
    } else {
      // Sans guillemets : les lignes suivantes plus indentées continuent la valeur.
      while (i + 1 < lignes.length && lignes[i + 1].trim() && lignes[i + 1].search(/\S/) > retrait) {
        brut += " " + lignes[++i].trim();
      }
      texte = brut;
    }
    texte = texte.trim();
    if (texte) textes.push(texte);
  }
  return textes;
}

/**
 * Textes dits par le tutoriel, relevés dans le code C# : champs consigne / consigneManettes /
 * bravo, et constantes « Phrase… ». (Les « panneau = … » ne sont qu'affichés.)
 */
export function lireTextesCode(contenu) {
  const motif = /(?:\b(?:consigne|consigneManettes|bravo)\s*=|\bconst\s+string\s+Phrase\w*\s*=)\s*"((?:[^"\\]|\\.)*)"/g;
  return [...contenu.matchAll(motif)].map((m) => decoderGuillemets(m[1]).trim()).filter(Boolean);
}

/** D'où viennent les textes, et quelle voix les dit (la première source qui contient un texte l'emporte). */
const SOURCES = [
  { nom: "narration", voix: VOIX_NARRATION, fichiers: SCENES, lire: lireRepliques },
  { nom: "tutoriel", voix: VOIX_TUTORIEL, fichiers: SCRIPTS_TUTORIEL, lire: lireTextesCode },
];

/** Même calcul que NarrationBulle.CleVoix : MD5 du texte (UTF-8) sans espaces autour. */
const cle = (texte) => createHash("md5").update(texte.trim(), "utf8").digest("hex");

/** Toutes les répliques : clé → { texte, source, voix }. */
function toutesLesRepliques() {
  const parCle = new Map();
  for (const source of SOURCES) {
    for (const fichier of source.fichiers) {
      if (!existsSync(fichier)) continue;
      for (const texte of source.lire(readFileSync(fichier, "utf8"))) {
        const c = cle(texte);
        if (!parCle.has(c)) parCle.set(c, { texte, source: source.nom, voix: source.voix });
      }
    }
  }
  return parCle;
}

const args = process.argv.slice(2);
const commande = args.find((a) => !a.startsWith("--")) ?? "liste";
const option = (nom) => args.find((a) => a.startsWith(`--${nom}=`))?.split("=")[1];
const refaire = args.includes("--refaire");

function exigerCle() {
  if (process.env.ELEVENLABS_API_KEY) return;
  console.error("ELEVENLABS_API_KEY absente : la définir, ou lancer « npx -y @elevenlabs/cli@latest onboard init ».");
  process.exit(1);
}

/** Écrit la lecture d'un texte dans un MP3. */
async function lire(client, voix, texte, chemin) {
  const audio = await client.textToSpeech.convert(voix, { text: texte, modelId: MODELE, languageCode: LANGUE });
  const morceaux = [];
  for await (const m of audio) morceaux.push(Buffer.from(m));
  writeFileSync(chemin, Buffer.concat(morceaux));
}

if (commande === "voix") {
  exigerCle();
  const { voices } = await new ElevenLabsClient().voices.getAll();
  for (const v of voices) {
    const l = v.labels ?? {};
    console.log(`${v.voiceId}  ${v.name}  (${v.category ?? "?"})  [${[l.gender, l.age, l.accent, l.use_case].filter(Boolean).join(", ")}]`);
  }
} else if (commande === "bibliotheque") {
  // Voix françaises de la bibliothèque publique ; --recherche=mot pour affiner.
  exigerCle();
  const { voices } = await new ElevenLabsClient().voices.getShared({
    pageSize: 30, language: "fr", gender: option("genre") ?? "male", search: option("recherche"),
  });
  for (const v of voices) {
    console.log(`${v.voiceId}  ${v.name}  [${[v.age, v.accent, v.useCase, v.descriptive].filter(Boolean).join(", ")}]`);
  }
} else if (commande === "essai") {
  // Même phrase pour chaque voix, à comparer : hors du projet Unity (dossier essais/, ignoré par git).
  exigerCle();
  const voix = (option("voix") ?? "").split(",").filter(Boolean);
  if (!voix.length) {
    console.error("Préciser les voix : npm run essai -- --voix=ID1,ID2");
    process.exit(1);
  }
  const client = new ElevenLabsClient();
  const dossier = join(ICI, "essais");
  mkdirSync(dossier, { recursive: true });
  // --source=tutoriel : une phrase du tutoriel plutôt que de la narration.
  const source = option("source") ?? "narration";
  const texte = [...toutesLesRepliques().values()].find((r) => r.source === source)?.texte;
  for (const v of voix) {
    await lire(client, v, texte, join(dossier, v + ".mp3"));
    console.log(`Essai écrit : essais/${v}.mp3`);
  }
  console.log(`Phrase lue : ${texte}`);
} else {
  const repliques = toutesLesRepliques();
  const existants = existsSync(SORTIE) ? new Set(readdirSync(SORTIE).filter((f) => f.endsWith(".mp3")).map((f) => f.slice(0, -4))) : new Set();
  const aFaire = [...repliques].filter(([c]) => refaire || !existants.has(c));
  const orphelins = [...existants].filter((c) => !repliques.has(c));

  for (const [c, r] of repliques) console.log(`${existants.has(c) ? "[voix]    " : "[manquante]"} (${r.source}) ${r.texte}`);
  const caracteres = aFaire.reduce((n, [, r]) => n + r.texte.length, 0);
  console.log(`\n${repliques.size} répliques, ${aFaire.length} à générer (${caracteres} caractères).`);
  const sansJeu = [...repliques.values()].filter((r) => !INTERPRETATION[r.texte]);
  if (sansJeu.length) console.log(`${sansJeu.length} réplique(s) sans interprétation (lues telles quelles).`);
  if (orphelins.length) console.log(`${orphelins.length} fichier(s) sans réplique (texte modifié ?) : ${orphelins.join(", ")}`);

  if (commande === "generer") {
    exigerCle();
    const client = new ElevenLabsClient();
    mkdirSync(SORTIE, { recursive: true });
    for (const [c, r] of aFaire) {
      const voix = option("voix") ?? r.voix;
      if (!voix) {
        console.log(`Pas de voix choisie pour « ${r.source} » (constante en tête de generer.mjs) : ${r.texte}`);
        continue;
      }
      await lire(client, voix, INTERPRETATION[r.texte] ?? r.texte, join(SORTIE, c + ".mp3"));
      console.log(`Généré (${r.source}) : ${r.texte}`);
    }
    // Index lisible : quel fichier dit quoi, avec quelle voix.
    const index = Object.fromEntries([...repliques].map(([c, r]) => [c, { texte: r.texte, source: r.source }]));
    const voix = Object.fromEntries(SOURCES.map((x) => [x.nom, x.voix]));
    writeFileSync(join(ICI, "index_voix.json"), JSON.stringify({ voix, modele: MODELE, repliques: index }, null, 2) + "\n");
  }
}
