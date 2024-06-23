import { useEffect, useRef, useState } from "react";

interface ModalProps {
  open: boolean;
  onClose: () => void;
}

function Modal({ open, onClose }: ModalProps) {
  const modalContainer = useRef<HTMLDivElement>(null);
  const [justMounded, setJustMounted] = useState(true);

  useEffect(() => {
    setJustMounted(false);
  }, []);
  function handleClose() {
    onClose();
  }

  if (justMounded) return null;

  return (
    <div
      ref={modalContainer}
      className={`fixed h-[100dvh] w-full top-0 left-0 flex justify-center items-center
    bg-white bg-opacity-50 transition-opacity duration-500 ${
      open ? "opacity-100" : "opacity-0 pointer-events-none"
    }`}
    >
      <div
        className="flex flex-col pt-2 px-6 pb-6 rounded-2xl h-fit max-w-[500px] w-full items-center mx-4 lg:mx-0
      bg-white shadow-lg border-2 border-[#f1f1f1]"
      >
        <button
          className="ml-auto text-2xl font-semibold"
          onClick={handleClose}
        >
          x
        </button>
        <p className="text-xl font-bold ">이해가 잘 되셨나요?</p>
        <ol className="list-decimal">
          <li className="">
            <p>어쩌구저쩌구</p>
          </li>
          <li className="">
            <p>어쩌구저쩌구</p>
          </li>
          <li className="">
            <p>어쩌구저쩌구</p>
          </li>
        </ol>
        <a
          href="https://open.kakao.com/o/sXJQ1qPd"
          target="_blank"
          className="block mt-7 bg-[#6151f3] px-4 py-2 lg:py-4 lg:px-7 rounded-full text-white font-semibold text-sm
            transition-transform hover:scale-105 duration-500"
        >
          1:1 문의 시작하기
        </a>
      </div>
    </div>
  );
}

export default Modal;
