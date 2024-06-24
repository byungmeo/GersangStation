import { Outlet } from "react-router-dom";

// import Tag from "./_components/Tag";

import background from "@/_assets/images/background.png";
// import imageTitleRound from "@/_assets/images/image_title_round.png";
// import handImage from "@/_assets/images/icon_hand.png";

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
  return (
    <>
      <div
        className="h-[100vh] w-full absolute lg:fixed bg-cover bg-center bg-no-repeat bg-[rgb(214,235,255)] z-[-1]"
        style={{
          backgroundImage: `url(${background})`,
        }}
      />

      <div className="max-w-[920px] h-fit mx-auto grid grid-cols-1 lg:grid-cols-2 font-['Noto_Sans_KR']">
        {/* 왼쪽 */}
        <div className="px-4 lg:p-0 pb-4 lg:pb-0 pt-3 lg:pt-0">
          <div className="lg:fixed h-full flex flex-col justify-center">
            <div className="font-semibold leading-[55px] lg:leading-[85px] text-[35px] lg:text-[60px] ">
              <p>다클 생성, 패치</p>
              <p>3분만에!</p>
              <div className="lg:mt-8">
{/*
                <img
                  src={imageTitleRound}
                  alt="토닥토닥"
                  className="absolute w-[260px] lg:w-[448px] -translate-y-1 lg:-translate-y-3 mx-auto z-[-1]"
                />
*/}                
                <p className="text-[#6151f3]">거상 스테이션</p>
              </div>
            </div>

            <a className="hidden lg:block mt-8 bg-[#6151f3] rounded-full text-xl font-bold text-white text-center px-7 py-4" href="https://github.com/byungmeo/GersangStation/releases/latest" target="_blank">
              설치하기
            </a>

            <ul className="hidden grid-cols-2 gap-3 mt-5 lg:grid">
              {exLinks.map((link, index) => (
                <li className="block" key={index}>
                  <a
                    className="block text-center bg-white rounded-md text-gray-800 py-1
                      border-[1px] border-gray-300 text-sm"
                    href={link.link}
                    target="_blank"
                  >
                    {link.title}
                  </a>
                </li>
              ))}
            </ul>
            
{/*
            <div className="space-y-3 mt-[30px]">
              <div className="flex items-center gap-2">
                <p className="text-xl font-bold">사용방법이 궁금하신가요?</p>
                <img src={handImage} alt="손 이미지" className="w-[24px]" />
              </div>
              <div className="flex gap-2">
                <Tag text="다클생성" />
                <Tag text="패치" />
                <Tag text="1분만에" />
              </div>
            </div>
*/}
          </div>
        </div>

        {/* 오른쪽 */}
        <div className="bg-white rounded-2xl lg:rounded-none lg:min-h-[100dvh] h-fit">
          <Outlet />
        </div>
      </div>
    </>
  );
}

export default SupportLayout;
