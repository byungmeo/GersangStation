function Footer() {
  return (
    <footer
      className="flex flex-col justify-center text-center bg-transparent z-[50] h-[250px]
          col-span-2"
    >
      <p
        className="text-indigo-700 font-[Dongle] font-bold
          text-[40px]"
      >
        거상 스테이션
      </p>
      <p className="mt-5 text-sm">
        © 2024-present Byungmeo. All Rights Reserved.
      </p>
      <p className="text-black/60 mt-2 text-xs">
        This website is designed and developed by Jehee Cheon.
      </p>
    </footer>
  );
}

export default Footer;
