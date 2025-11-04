(function(){if(typeof window!=='undefined'&&window.bootstrap&&bootstrap.Carousel){return;}
function initCarousel(root){const interval=Number(root.dataset.bsInterval||5000);const wrap=(root.dataset.bsWrap||'true')!=='false';
const items=[...root.querySelectorAll('.carousel-item')];const dots=[...root.querySelectorAll('[data-bs-slide-to]')];
const prev=root.querySelector('[data-bs-slide="prev"]');const next=root.querySelector('[data-bs-slide="next"]');
let i=Math.max(0,items.findIndex(x=>x.classList.contains('active')));if(i<0){i=0;items[0]?.classList.add('active');dots[0]?.classList.add('active');}
function show(n){if(!items.length)return;if(n<0)n=wrap?items.length-1:0;if(n>=items.length)n=wrap?0:items.length-1;if(n===i)return;
items[i]?.classList.remove('active');dots[i]?.classList.remove('active');i=n;items[i].classList.add('active');dots[i]?.classList.add('active');}
dots.forEach((b,idx)=>b.addEventListener('click',e=>{e.preventDefault();show(idx);}));prev?.addEventListener('click',e=>{e.preventDefault();show(i-1);});
next?.addEventListener('click',e=>{e.preventDefault();show(i+1);}));let t;function start(){stop();t=setInterval(()=>show(i+1),interval);}function stop(){if(t){clearInterval(t);t=null;}}
root.addEventListener('mouseenter',stop);root.addEventListener('mouseleave',start);document.addEventListener('visibilitychange',()=>{if(document.hidden)stop();else start();});start();}
document.addEventListener('DOMContentLoaded',()=>document.querySelectorAll('#homeCarousel').forEach(initCarousel));})();