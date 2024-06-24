import { Outlet } from "react-router-dom";

import Tag from "@/_components/Tag";
import BgParticles from "@/_components/BgParticles";
import { useEffect, useRef } from "react";

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

  function calvLeftPannelPosition() {
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
    document.addEventListener("scroll", calvLeftPannelPosition);
    document.addEventListener("click", calvLeftPannelPosition);
    document.addEventListener("touchend", calvLeftPannelPosition);
    document.addEventListener("resize", calvLeftPannelPosition);
    document.addEventListener("orientationchange", calvLeftPannelPosition);

    return () => {
      document.removeEventListener("scroll", calvLeftPannelPosition);
      document.removeEventListener("click", calvLeftPannelPosition);
      document.removeEventListener("touchend", calvLeftPannelPosition);
      document.removeEventListener("resize", calvLeftPannelPosition);
      document.removeEventListener("orientationchange", calvLeftPannelPosition);
    };
  }, []);

  return (
    <div className="font-['Noto_Sans_KR']">
      <div
        className="h-full min-h-[100dvh] lg:max-w-[1000px] xl:max-w-[1200px] mx-auto 
        lg:grid grid-cols-2 gap-x-[80px]"
      >
        {/* 왼쪽 */}
        <div className="px-4 lg:p-0 pb-4 lg:pb-0 pt-3 lg:pt-0">
          <div
            ref={leftPannelRef}
            className="lg:fixed h-fit lg:h-full flex flex-col justify-center w-full lg:max-w-[460px] xl:max-w-[560px]
            transition-all duration-1000"
          >
            <div className="flex gap-2 ">
              <Tag text="다클생성" />
              <Tag text="패치" />
              <Tag text="커서고정" />
            </div>

            <p
              className="text-[75px] lg:text-[90px] xl:text-[110px] font-semibold 
              bg-gradient-to-r from-[#d141ff] to-orange-400 bg-clip-text text-transparent font-[Dongle]"
            >
              거상 스테이션
            </p>

            <div className="flex gap-3 flex-col lg:flex-row lg:mt-8">
              <a
                className="hidden lg:block w-full bg-pink-400 rounded-full text-xl font-bold text-white text-center px-7 py-4
                transition-all hover:scale-105 duration-500 hover:bg-pink-500/90"
                href="https://github.com/byungmeo/GersangStation/releases/latest"
                target="_blank"
              >
                설치하기
              </a>
              <a
                className="text-nowrap bg-pink-400 lg:bg-transparent rounded-full text-xl font-bold text-white lg:text-pink-400 text-center 
                px-7 py-3 lg:py-4 border-[1px] border-pink-400 transition-all hover:scale-[103%] lg:hover:scale-105 duration-500 lg:hover:bg-pink-50/50"
                href="https://github.com/byungmeo/GersangStation/releases/latest"
                target="_blank"
              >
                프로그램 소개
              </a>
            </div>

            <ul className="hidden grid-cols-2 gap-3 mt-5 lg:grid">
              {exLinks.map((link, index) => (
                <li className="block" key={index}>
                  <a
                    className="block text-center bg-transparent rounded-md text-pink-400 py-1
                      border-[1px] border-pink-200 text-sm transition-all hover:scale-105 duration-500 hover:bg-pink-50/50"
                    href={link.link}
                    target="_blank"
                  >
                    {link.title}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* 오른쪽 */}
        <div
          className="bg-white lg:bg-transparent rounded-2xl lg:rounded-none lg:min-h-[100dvh] w-full h-full
          ounded-t-2xl border-[1px] overflow-hidden border-gray-200"
        >
          <Outlet />
        </div>

        <footer
          className="flex flex-col justify-center text-center bg-transparent z-[50] h-[250px]
          col-span-2"
        >
          <p
            className="bg-gradient-to-r from-[#d141ff] to-orange-400 bg-clip-text text-transparent font-[Dongle] font-bold
          text-[40px]"
          >
            거상 스테이션
          </p>
          <p className="mt-5">© 2024-present Byungmeo. All Rights Reserved.</p>
          <p className="text-black/60 mt-2">
            This website is designed and developed by Jehee Cheon
          </p>
        </footer>
      </div>

      <BgParticles />
    </div>
  );
}

export default SupportLayout;
