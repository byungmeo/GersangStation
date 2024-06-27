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
    calvLeftPannelPosition();
    document.addEventListener("scroll", calvLeftPannelPosition);
    document.addEventListener("resize", calvLeftPannelPosition);
    document.addEventListener("orientationchange", calvLeftPannelPosition);

    return () => {
      document.removeEventListener("scroll", calvLeftPannelPosition);
      document.removeEventListener("resize", calvLeftPannelPosition);
      document.removeEventListener("orientationchange", calvLeftPannelPosition);
    };
  }, []);

  return (
    <div className="font-['Noto_Sans_KR'] h-full">
      <div
        className="h-full lg:max-w-[1000px] xl:max-w-[1200px] 2xl:max-w-[1500px]
        lg:grid grid-cols-2 gap-x-[80px] flex flex-col md:mx-5 lg:mx-auto"
      >
        {/* 왼쪽 */}
        <div className="my-[40px] lg:my-0 px-4 lg:p-0 pb-4 lg:pb-0 pt-3 lg:pt-0 lg:animate-fade-in-slow">
          <div
            ref={leftPannelRef}
            className="lg:fixed h-fit lg:h-full flex flex-col justify-center w-full lg:max-w-[460px] xl:max-w-[560px] 2xl:max-w-[640px]
            transition-all duration-1000"
          >
            <div className="flex flex-col-reverse lg:flex-col">
              <div className="flex gap-2 ">
                <Tag text="다클생성" />
                <Tag text="패치" />
                <Tag text="커서고정" />
              </div>

              <p
                className="text-[55px] lg:text-[90px] xl:text-[110px] font-semibold 
                text-indigo-700 font-[Dongle]"
              >
                거상 스테이션
              </p>
            </div>

            <div className="hidden gap-3 flex-col lg:flex-row lg:mt-8 lg:flex ">
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
          </div>
        </div>

        {/* 오른쪽 */}
        <div
          className="bg-white lg:bg-transparent rounded-2xl lg:rounded-none w-full h-fit lg:max-w-[460px] xl:max-w-[560px] 2xl:max-w-[710px] box-content
          border-[2px] lg:border-[1px] border-gray-200 mb-[30px] lg:my-0 lg:animate-show-from-right"
        >
          <Outlet />
        </div>

        <Footer />
      </div>

      <BgParticles />
    </div>
  );
}

export default SupportLayout;
