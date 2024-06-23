import { Outlet } from "react-router-dom";

import Tag from "./_components/Tag";

import background from "@/_assets/images/background.png";
import imageTitleRound from "@/_assets/images/image_title_round.png";
import handImage from "@/_assets/images/icon_hand.png";

function SupportLayout() {
  return (
    <>
      <div
        className="h-[100vh] w-full absolute bg-cover bg-center bg-no-repeat bg-[rgb(214,235,255)] z-[-1]"
        style={{
          backgroundImage: `url(${background})`,
        }}
      />

      <div className="h-[100dvh] w-[920px] mx-auto grid grid-cols-2 font-['Noto_Sans_KR']">
        {/* 왼쪽 */}
        <div className="my-auto space-y-[60px]">
          <div className="text-[60px] font-semibold leading-[85px]">
            <div>
              <img
                src={imageTitleRound}
                alt="토닥토닥"
                className="absolute w-[224px] -translate-y-3 mx-auto z-[-1]"
              />
              토닥토닥
            </div>
            <p>사용 방법</p>
            <p className="text-[#6151f3]">거상 스테이션</p>
          </div>

          <div className="space-y-3">
            <div className="flex items-center gap-2">
              <p className="text-xl font-bold">사용방법이 궁금하신가요?</p>
              <img src={handImage} alt="손 이미지" className="w-[24px]" />
            </div>

            <div className="flex gap-2">
              <Tag text="둘둘둘둘" />
              <Tag text="셋셋셋셋" />
              <Tag text="하나" />
            </div>
          </div>
        </div>

        {/* 오른쪽 */}
        <div className="bg-white">
          <div className="p-4">

          </div>

          
          <Outlet />
        </div>
      </div>
    </>
  );
}

export default SupportLayout;
