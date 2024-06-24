interface TagProps {
  text: string;
}

function Tag({ text }: TagProps) {
  return (
    <div
      className="bg-transparent border-[1px] border-pink-200 rounded-full px-4 py-[6px] 
      text-pink-400 font-semibold"
    >
      {text}
    </div>
  );
}

export default Tag;
