import { SectionHead } from '../components/HomeParts';
import { Hero } from '../components/HomeHero';
import { Events, LatestNews } from '../components/HomeEvents';
import { Music, Gallery } from '../components/HomeMedia';
import { JoinUs, Doors, InstallApp } from '../components/HomeJoin';

export function Home() {
  return (
    <>
      <Hero />
      <About />
      <Events />
      <LatestNews />
      <Music />
      <Gallery />
      <JoinUs />
      <Doors />
      <InstallApp />
    </>
  );
}

// ---------- about ----------

function About() {
  return (
    <section id="about" className="section section--raise about" aria-labelledby="about-title">
      <div className="wrap about__grid">
        <SectionHead id="about-title" eyebrow="Quem somos" title="A tuna masculina de Bragança">
          Fundada em 1991 no então IPB, hoje UPB, a RTUB junta estudantes à volta da guitarra, do bandolim e da capa
          negra, e leva essa música para a rua.
        </SectionHead>
        <ul className="about__points">
          <li>
            <h3 className="about__point">Serenatas</h3>
            <p>À janela, na praça ou no palco, como a tradição académica pede.</p>
          </li>
          <li>
            <h3 className="about__point">Vida académica</h3>
            <p>Receções, festas e noites que marcam o percurso de quem estuda em Bragança.</p>
          </li>
          <li>
            <h3 className="about__point">Bragança</h3>
            <p>O castelo no emblema diz de onde vimos e para onde voltamos sempre.</p>
          </li>
        </ul>
      </div>
    </section>
  );
}
