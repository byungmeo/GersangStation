import { Outlet } from "react-router-dom";

import Tag from "@/_components/Tag";
import BgParticles from "@/_components/BgParticles";
import { useEffect, useRef } from "react";
import Footer from "@/_components/Footer";

const exLinks: {
  title: string;
  link: string;
}[] = [
  {
    title: "설치/사용설명서",
    link: "https://github.com/byungmeo/GersangStation/wiki/%EC%82%AC%EC%9A%A9%EC%9E%90-%EC%84%A4%EB%AA%85%EC%84%9C",
  },
  {
    title: ".NET SDK",
    link: "https://dotnet.microsoft.com/en-us/download/dotnet/6.0",
  },
  {
    title: "Edge Beta",
    link: "https://go.microsoft.com/fwlink/?linkid=2100017&Channel=Beta&language=ko&brand=M103",
  },
  {
    title: "원격지원",
    link: "ms-quick-assist://",
  },
];

function SupportLayout() {
  const leftPannelRef = useRef<HTMLDivElement>(null);

  function calcLeftPannelPosition() {
    const footer = document.querySelector("footer");
    const footerY = document.body.scrollHeight - footer!.clientHeight;
    const scrollBottom = window.scrollY + window.innerHeight;
    const diff = scrollBottom - footerY;

    if (diff > 0) {
      leftPannelRef.current!.style.bottom = `${diff}px`;
    } else {
      leftPannelRef.current!.style.bottom = `0px`;
    }
  }

  useEffect(() => {
    calcLeftPannelPosition();
    document.addEventListener("scroll", calcLeftPannelPosition);
    window.addEventListener("resize", calcLeftPannelPosition);
    document.addEventListener("orientationchange", calcLeftPannelPosition);

    return () => {
      document.removeEventListener("scroll", calcLeftPannelPosition);
      window.removeEventListener("resize", calcLeftPannelPosition);
      document.removeEventListener("orientationchange", calcLeftPannelPosition);
    };
  }, []);

  return (
    <div
      className="font-['Noto_Sans_KR'] h-full w-full"
      onLoad={() => calcLeftPannelPosition()}
    >
      <div
        className="h-full w-full lg:mx-auto lg:px-[5vw] xl:px-[10vw]  
        lg:grid lg:grid-cols-[460px_auto] xl:grid-cols-[560px_auto] 2xl:grid-cols-[640px_auto] gap-x-[80px] 
        flex flex-col md:mx-5"
      >
        {/* 왼쪽 */}
        <aside className="my-[50px] lg:my-0 lg:px-0 px-4 lg:animate-fade-in-slow">
          <div
            ref={leftPannelRef}
            className="lg:fixed h-full flex flex-col justify-center w-full 
            lg:max-w-[460px] xl:max-w-[560px] 2xl:max-w-[640px]
            transition-all duration-1000"
          >
            <section className="flex flex-col-reverse lg:flex-col">
              <div className="flex gap-2 ">
                <Tag text="다클생성" />
                <Tag text="패치" />
                <Tag text="3분만에" />
              </div>

              <h1
                className="text-[55px] lg:text-[90px] xl:text-[110px] font-semibold 
                text-indigo-700 font-[Dongle]"
              >
                거상 스테이션
              </h1>
            </section>

            <section>
              <nav>
                <ul className="hidden gap-3 flex-col lg:flex-row lg:mt-8 lg:flex ">
                  <a
                    className="w-full bg-indigo-600 rounded-full text-xl font-bold text-white text-center px-7 py-4
                    transition-all hover:scale-105 duration-500 hover:bg-indigo-600/90"
                    href="https://github.com/byungmeo/GersangStation/releases/latest"
                    target="_blank"
                  >
                    설치하기
                  </a>
                  <a
                    className="text-nowrap bg-indigo-600 lg:bg-transparent rounded-full text-xl font-bold text-white lg:text-indigo-600 text-center 
                    px-7 py-3 lg:py-4 border-[1px] border-indigo-600 transition-all hover:scale-[103%] lg:hover:scale-105 duration-500 lg:hover:bg-indigo-50/50"
                    href="https://github.com/byungmeo/GersangStation"
                    target="_blank"
                  >
                    GitHub
                  </a>
                </ul>

                <ul className="hidden grid-cols-2 gap-3 mt-5 lg:grid">
                  {exLinks.map((link, index) => (
                    <li className="block" key={index}>
                      <a
                        className="block text-center bg-transparent rounded-md text-indigo-600 py-1
                        border-[1px] border-indigo-600 text-sm transition-all hover:scale-105 duration-500 hover:bg-indigo-50/50"
                        href={link.link}
                        target="_blank"
                      >
                        {link.title}
                      </a>
                    </li>
                  ))}
                </ul>
              </nav>
            </section>
          </div>
        </aside>

        {/* 오른쪽 */}
        <main
          className="bg-white lg:bg-transparent rounded-2xl lg:rounded-none w-full max-w-[1000px] mx-auto h-fit
          lg:my-0 lg:animate-show-from-right
          border-[1.5px] lg:border-[1px] border-gray-200"
        >
          <Outlet />
        </main>

        <Footer className="col-span-2 mt-auto shrink-0" />
      </div>

      <BgParticles />
    </div>
  );
}

export default SupportLayout;
